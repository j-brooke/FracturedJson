# FracturedJson

FracturedJson is utility that formats JSON data in a user-readable but fairly compact way. Arrays and objects are written on single lines if they're short enough and not too complex; otherwise their contents are presented indented, beginning on lines of their own.

It is available as a browser form, a .NET Core 3.1 library, a Javascript package, and a Visual Studio Code extension.

Here's a sample of output using default settings:
```json
{
    "SimpleArray": [
          2,   3,   5,   7,  11,  13,  17,  19,  23,  29,  31,  37,  41,  43,  47,  53,
         59,  61,  67,  71,  73,  79,  83,  89,  97, 101, 103, 107, 109, 113
    ],
    "ObjectColumnsArrayRows": {
        "Katherine": [ "blue"      , "lightblue", "black"        ],
        "Logan"    : [ "yellow"    , "blue"     , "black", "red" ],
        "Erik"     : [ "red"       , "purple"                    ],
        "Jean"     : [ "lightgreen", "yellow"   , "black"        ]
    },
    "ArrayColumnsObjectRows": [
        { "type": "turret"   , "hp": 400, "loc": {"x": 47, "y": -4} , "flags": "S"   },
        { "type": "assassin" , "hp":  80, "loc": {"x": 12, "y": 6}  , "flags": "Q"   },
        { "type": "berserker", "hp": 150, "loc": {"x": 0, "y": 0}                    },
        { "type": "pittrap"  ,            "loc": {"x": 10, "y": -14}, "flags": "S,I" }
    ],
    "ComplexArray": [
        [ 19,  2 ],
        [  3,  8 ],
        [ 14,  0 ],
        [  9,  9 ],
        [  9,  9 ],
        [  0,  3 ],
        [ 10,  1 ],
        [  9,  1 ],
        [  9,  2 ],
        [  6, 13 ],
        [ 18,  5 ],
        [  4, 11 ],
        [ 12,  2 ]
    ]
}
```

## More Information

Please see the [project wiki](https://github.com/j-brooke/FracturedJson/wiki) for more information on what you can do and how to use the tools.
