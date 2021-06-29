# FracturedJson

FracturedJson is utility that formats JSON data in a user-readable but fairly compact way. Arrays and objects are written on single lines if they're short enough and not too complex; otherwise their contents are presented indented, beginning on lines of their own.

It is available as a browser form, a commandline app, a .NET Core 3.1 library, a Javascript package, and a Visual Studio Code extension.

Here's a brief, highly contrived example of the output:
```json
{
    "SimpleItem": 77,
    "ShortArray": ["blue", "blue", "orange", "gray"],
    "LongArray": [
        2, 3, 5, 7, 11, 13, 17, 19, 23, 29, 31, 37, 41, 43, 47, 53, 59, 61, 67, 71, 73,
        79, 83, 89, 97, 101, 103, 107, 109, 113, 127, 131, 137, 139, 149, 151, 157,
        163, 167, 173, 179, 181, 191, 193, 197, 199, 211, 223, 227, 229, 233, 239, 241,
        251, 257, 263, 269, 271, 277, 281, 283, 293
    ],
    "ComplexObject": {
        "Subthing1": {"X": 55, "Y": 19, "Z": -4},
        "Subthing2": { "Q": null, "W": [-2, -1, 0, 1] },
        "Distraction": [[], null, null]
    }
}
```

## More Information

Please see the [project wiki](https://github.com/j-brooke/FracturedJson/wiki) for more information on what you can do and how to use the tools.
