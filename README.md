# FracturedJson

FracturedJson is utility that formats JSON data in a user-readable but fairly compact way. Arrays and objects are written on single lines if they're short enough and not too complex; otherwise their contents are presented indented, beginning on lines of their own.

It is available as a .NET Core 3.1 library, and as a .NET Core 3.1 commandline app.

Here's a brief, highly contrived example of the output:
```json
{
    "SimpleItem": 77,
    "ShortArray": ["blue", "blue", "orange", "gray"],
    "LongArray": [
        2, 3, 5, 7, 11, 13, 17, 19, 23, 29, 31, 37, 41, 43, 47, 53, 59, 61, 67, 71, 73, 79, 83,
        89, 97, 101, 103, 107, 109, 113, 127, 131, 137, 139, 149, 151, 157, 163, 167, 173, 179,
        181, 191, 193, 197, 199, 211, 223, 227, 229, 233, 239, 241, 251, 257, 263, 269, 271, 277,
        281, 283, 293, 307, 311, 313, 317, 331, 337, 347, 349, 353, 359, 367, 373, 379, 383, 389,
        397, 401, 409, 419, 421, 431, 433, 439, 443, 449, 457, 461, 463, 467, 479, 487, 491, 499,
        503, 509, 521, 523, 541, 547, 557, 563, 569, 571, 577, 587, 593, 599, 601, 607, 613, 617,
        619, 631, 641, 643, 647, 653, 659, 661, 673, 677, 683, 691, 701, 709, 719, 727, 733, 739,
        743, 751, 757, 761, 769, 773, 787, 797, 809, 811, 821, 823, 827, 829, 839, 853, 857, 859,
        863, 877, 881, 883, 887, 907, 911, 919, 929, 937, 941, 947, 953, 967, 971, 977, 983, 991,
        997
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
