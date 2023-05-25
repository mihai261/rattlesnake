import os
import ast
import sys
import json

imports_list = []
classes_list = []
methods_list = []
calls_list = {}


class Project:
    def __init__(self, name, folders):
        self.name = name
        self.folders = folders


class Folder:
    def __init__(self, name, files):
        self.name = name
        self.files = files


class SourceFile:
    def __init__(self, name, lines, imports, classes, methods):
        self.name = name
        self.lines = lines
        self.imports = imports
        self.classes = classes
        self.methods = methods


class Lines():
    def __init__(self, lines_total, lines_code, lines_commented, lines_docs, lines_empty):
        self.lines_total = lines_total
        self.lines_code = lines_code
        self.lines_commented = lines_commented
        self.lines_docs = lines_docs
        self.lines_empty = lines_empty


class Method:
    def __init__(self, name, lines,  complexity, decorators, args):
        self.name = name
        self.lines = lines
        self.complexity = complexity
        self.decorators = decorators
        self.parent = "*"
        self.args = args
        self.sub_calls = []
    
    @classmethod
    def with_parent_class(self, name, lines, complexity, class_name, decorator, args):
        mth = self(name, lines, complexity, decorator, args)
        mth.parent = class_name
        return mth


class Argument:
    def __init__(self, name, annotation):
        self.name = name
        self.annotation = annotation

class Assignment:
    def __init__(self, variable_name, class_name):
        self.variable_name = variable_name
        self.class_name = class_name


class Class:
    def __init__(self, name, lines, object_assignments):
        self.name = name
        self.lines = lines
        self.methods = []
        self.object_assignments = object_assignments

    @classmethod
    def with_superclass(self, name, lines, object_assignments, super_classes):
        cls = self(name, lines, object_assignments)
        cls.super_classes = super_classes
        return cls


class Analyzer(ast.NodeVisitor):
    def visit_Import(self, node):
        global imports_list

        for alias in node.names:
            imports_list.append(alias.name)

        self.generic_visit(node)

    def visit_ImportFrom(self, node: ast.ImportFrom):
        global imports_list

        for alias in node.names:
            imports_list.append('%s.%s' % (node.module, alias.name))

        self.generic_visit(node)

    def visit_ClassDef(self, node: ast.ClassDef):
        global classes_list
        global code
        object_assignments = set()

        code.seek(0)
        lines = count_lines(code, node.lineno, node.end_lineno)

        for statement in node.body:
            if isinstance(statement, ast.Assign):
                if len(statement.targets) == 1 and isinstance(statement.targets[0], ast.Name):
                    if isinstance(statement.value, ast.Call):
                        func_name = "error_parsing_in_Call_node"
                        if isinstance(statement.value.func, ast.Name):
                            func_name = statement.value.func.id
                        object_assignments.add(Assignment(statement.targets[0].id, func_name))
                    else:
                        object_assignments = {element for element in object_assignments if element.variable_name != statement.targets[0].id}
                
        if node.bases != []:
            bases_list = []
            for base in node.bases:
                if isinstance(base, ast.Attribute):
                    bases_list.append(base.value.id + "." + base.attr)
                else:
                    bases_list.append(base.id)
            classes_list.append(Class.with_superclass(node.name, lines, object_assignments, bases_list))
        else:
            classes_list.append(Class(node.name, lines, object_assignments))

        self.generic_visit(node)

    
    def calculate_node_complexity(self, node):
        node_complexity = 0
        if isinstance(node, ast.If) or isinstance(node, ast.For) or isinstance(node, ast.While) or isinstance(node, ast.BoolOp) or isinstance(node, ast.ExceptHandler) or isinstance(node, ast.With) or isinstance(node, ast.Assert) or isinstance(node, ast.comprehension): 
                node_complexity += 1
        for child_node in ast.iter_child_nodes(node):
            node_complexity += self.calculate_node_complexity(child_node)
        
        return node_complexity


    def visit_FunctionDef(self, node: ast.FunctionDef):
        global code
        code.seek(0)
        lines = count_lines(code, node.lineno, node.end_lineno)

        # calculate cyclomatic complexity
        method_complexity = 1
        for child_node in ast.iter_child_nodes(node):
            method_complexity += self.calculate_node_complexity(child_node)

        for item in node.body:
            self.generic_visit(item)

        decorator_names_list = []
        for decorator in node.decorator_list:
            if isinstance(decorator, ast.Attribute):
                decorator_names_list.append(decorator.value.id + "." + decorator.attr)
            else:
                decorator_names_list.append(decorator.id)

        args_list = []
        for arg in node.args.args:
            if isinstance(arg.annotation, ast.Attribute):
                args_list.append(Argument(arg.arg, parse_attribute(arg.annotation)).__dict__)
            if isinstance(arg.annotation, ast.Name):
                args_list.append(Argument(arg.arg, arg.annotation.id).__dict__)

        if type(node) != ast.Module:
            node_parent = node.parent
            while (type(node_parent) != ast.ClassDef) and (type(node_parent) != ast.Module):
                node_parent = node_parent.parent
            if type(node_parent) == ast.ClassDef:
                for iclass in classes_list:
                    if iclass.name == node_parent.name:
                        iclass.methods.append(Method.with_parent_class(node.name, lines, method_complexity, iclass.name, decorator_names_list, args_list))
            else:
                methods_list.append(Method(node.name, lines, method_complexity, decorator_names_list, args_list))

        self.generic_visit(node)

    def visit_Call(self, node: ast.Call):
        func_name = "error_parsing_in_Call_node"
        if isinstance(node.func, ast.Name):
            func_name = node.func.id
        elif isinstance(node.func, ast.Attribute):
            func_name = parse_attribute(node.func)

        if type(node) != ast.Module:
            node_parent = node.parent
            while (type(node_parent) != ast.FunctionDef) and (type(node_parent) != ast.Module):
                node_parent = node_parent.parent
            if type(node_parent) == ast.FunctionDef:
                node_parent.inner_calls.add(func_name)
                calls_list[node_parent.name] = node_parent.inner_calls

        self.generic_visit(node)


def parse_attribute(attribute: ast.Attribute):
    if isinstance(attribute.value, ast.Name):
        return attribute.value.id + "." + attribute.attr
    elif isinstance(attribute.value, ast.Attribute):
        return parse_attribute(attribute.value) + "." + attribute.attr
    else:
        return "error_parsing_in_Attr_node"


def count_lines(code, start_line, end_line):
    lines_total = 0
    lines_code = 0
    lines_commented = 0
    lines_docs = 0
    lines_empty = 0
    in_docstring = False
    for i in range(1, start_line):
        next(code)
    for i in range(1, end_line - start_line + 1):
        line = next(code)
        line = line.strip()
        lines_total += 1
        if (("\"\"\"" in line) or ("\'\'\'" in line)) and len(line) > 6:
            lines_docs += 1
        else:
            if (("\"\"\"" in line) or ("\'\'\'" in line)) and in_docstring:
                lines_docs += 1
                in_docstring = False
            else:
                if ("\"\"\"" in line) or ("\'\'\'" in line):
                    in_docstring = True

                if in_docstring:
                    lines_docs += 1
                elif len(line) != 0 and line[0] == '#':
                    lines_commented += 1
                elif len(line) == 0:
                    lines_empty += 1
                else:
                    lines_code += 1

    # reset file read pointer
    code.seek(0)
    return Lines(lines_total=lines_total, lines_code=lines_code, lines_commented=lines_commented, lines_docs=lines_docs,
                 lines_empty=lines_empty).__dict__


def main():
    global imports_list
    global classes_list
    global methods_list
    global calls_list
    global code

    analyzer = Analyzer()

    # TODO THIS IS FOR DEBBUGING; REMOVE WHEN DONE
    if os.path.isfile(sys.argv[1]):
        with open(sys.argv[1], 'r') as code:
            syntax_tree = ast.parse(code.read())
            print(ast.dump(syntax_tree, indent=4))
            return

    projectName = sys.argv[1].split("/")[-1]
    if projectName == "": projectName = sys.argv[1].split("/")[-2]
    payload = Project(projectName, [])

    for (dirName, files) in [(d, f) for d, s, f in os.walk(sys.argv[1]) if not d.startswith("./.")]:
        relativePath = dirName.split(sys.argv[1])[-1]
        if relativePath.startswith("/") or relativePath == "":
            relativePath = "." + relativePath
        else:
            relativePath = "./" + relativePath
        dirData = Folder(relativePath, [])
        for file in [f for f in files if f.endswith(".py")]:
            with open(os.path.join(dirName, file), 'r') as source:
                for line_count, line in enumerate(source):
                    pass
                source.seek(0)
                code = source
                lines_json = count_lines(code, 0, line_count+1)

                imports_list = []
                classes_list = []
                methods_list = []
                calls_list = {}

                syntax_tree = ast.parse(code.read())
                # print(ast.dump(syntax_tree, indent=4))
                for node in ast.walk(syntax_tree):
                    for child in ast.iter_child_nodes(node):
                        child.parent = node
                    if isinstance(node, ast.FunctionDef):
                        node.inner_calls = set()

                analyzer.visit(syntax_tree)
                for key in calls_list:
                    for iclass in classes_list:
                        for method in iclass.methods:
                            if method.name == key:
                                method.sub_calls = list(calls_list[key])
                    for method in methods_list:
                        if method.name == key:
                            method.sub_calls = list(calls_list[key])

                classes_list_json = []
                for iclass in classes_list:
                    iclass.methods = [method.__dict__ for method in iclass.methods]
                    iclass.object_assignments = [assignment.__dict__ for assignment in iclass.object_assignments]
                    classes_list_json.append(iclass.__dict__)

                methods_list_json = [method.__dict__ for method in methods_list]

            dirData.files.append(SourceFile(file, lines=lines_json, imports=imports_list, classes=classes_list_json,
                                            methods=methods_list_json).__dict__)

        payload.folders.append(dirData.__dict__)
        # break

    json_payload = json.dumps(payload.__dict__, indent=4)

    file_path = "./results/probe_results.json"
    os.makedirs(os.path.dirname(file_path), exist_ok=True)
    with open(file_path, "w") as f:
        f.write(json_payload)


if __name__ == "__main__":
    main()
