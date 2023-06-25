import time

'''single line doc again'''
class ObjectType1:
    prefix = 'a'

    def method_x(self, a: str) -> bool:
        return a.startswith(self.prefix)

    @classmethod
    def method_y(cls, b: str) -> float:
        return time.time().real - len(b)

'''
multiline docstring
'''
class Base1:
    obj1 = ObjectType1()

    def __init__(self, arg1: int, arg2: str):
        self.prop1 = arg1,
        self.prop2 = arg2

    # some comment
    def method1(self) -> bool:
        return not self.obj1.method_x(self.prop2)


class Base2:
    def __init__(self, arg1: int, arg2: str):
        self.prop1 = arg1,
        self.prop2 = arg2
        
    # multiple
    # commented
    # lines
    def method2(self) -> float:
        return int(ObjectType1.method_y(self.prop2)) / self.prop1[0]
