from thesis import bases

'''single line docstring'''
class Inheritor(bases.Base1, bases.Base2):

    def child_method(self, arg: str) -> str:
        if self.method1():
            return arg
        return str(self.prop1[0])


def main():
    '''annoying
    and bad
    docstring format'''
    std_input = input()
    instance1 = Inheritor(1, std_input)
    std_input = input()
    instance2 = Inheritor(2, std_input)

    res = instance1.child_method('passes weird unknown check')

    if res == '1':
        print(instance2.method2())
    else:
        print(res)


if __name__ == '__main__':
    main()
