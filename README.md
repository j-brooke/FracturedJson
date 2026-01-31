# FracturedJson

FracturedJson is a family of utilities that format [JSON data](https://www.json.org) in a way that's easy for
humans to read, but fairly compact.  Arrays and objects are written on single lines, as long as they're
neither too long nor too complex.  When several such lines are similar in structure, they're written with
fields aligned like a table.  Long arrays are written with multiple items per line across multiple lines.

Here's a sample of output using nearly default settings. (`MaxTotalLineLength=100`)
```json
{
    "BasicObject"   : {
        "ModuleId"   : "armor",
        "Name"       : "",
        "Locations"  : [
            [11,  2], [11,  3], [11,  4], [11,  5], [11,  6], [11,  7], [11,  8], [11,  9],
            [11, 10], [11, 11], [11, 12], [11, 13], [11, 14], [ 1, 14], [ 1, 13], [ 1, 12],
            [ 1, 11], [ 1, 10], [ 1,  9], [ 1,  8], [ 1,  7], [ 1,  6], [ 1,  5], [ 1,  4],
            [ 1,  3], [ 1,  2], [ 4,  2], [ 5,  2], [ 6,  2], [ 7,  2], [ 8,  2], [ 8,  3],
            [ 7,  3], [ 6,  3], [ 5,  3], [ 4,  3], [ 0,  4], [ 0,  5], [ 0,  6], [ 0,  7],
            [ 0,  8], [12,  8], [12,  7], [12,  6], [12,  5], [12,  4]
        ],
        "Orientation": "Fore",
        "Seed"       : 272691529
    },
    "SimilarArrays" : {
        "Katherine": ["blue",       "lightblue", "black"       ],
        "Logan"    : ["yellow",     "blue",      "black", "red"],
        "Erik"     : ["red",        "purple"                   ],
        "Jean"     : ["lightgreen", "yellow",    "black"       ]
    },
    "SimilarObjects": [
        { "type": "turret",    "hp": 400, "loc": {"x": 47, "y":  -4}, "flags": "S"   },
        { "type": "assassin",  "hp":  80, "loc": {"x": 12, "y":   6}, "flags": "Q"   },
        { "type": "berserker", "hp": 150, "loc": {"x":  0, "y":   0}                 },
        { "type": "pittrap",              "loc": {"x": 10, "y": -14}, "flags": "S,I" }
    ]
}
```

If enabled in the settings, it can also handle JSON-with-comments.

```jsonc
{
    /*
     * Multi-line comments
     * are fun!
     */
    "NumbersWithHex": [
          254 /*00FE*/,  1450 /*5AA*/,      0 /*0000*/, 36000 /*8CA0*/,    10 /*000A*/,
          199 /*00C7*/, 15001 /*3A99*/,  6540 /*198C*/
    ],
    /* Elements are keen */
    "Elements"      : [
        { /*Carbon*/   "Symbol": "C",  "Number":  6, "Isotopes": [11, 12, 13, 14] },
        { /*Oxygen*/   "Symbol": "O",  "Number":  8, "Isotopes": [16, 18, 17    ] },
        { /*Hydrogen*/ "Symbol": "H",  "Number":  1, "Isotopes": [ 1,  2,  3    ] },
        { /*Iron*/     "Symbol": "Fe", "Number": 26, "Isotopes": [56, 54, 57, 58] }
        // Not a complete list...
    ],

    "Beatles Songs" : [
        "Taxman",          // George
        "Hey Jude",        // Paul
        "Act Naturally",   // Ringo
        "Ticket To Ride"   // John
    ]
}
```

## Repo Contents

This repo is home to the .NET versions of FracturedJson:

* [FracturedJson .NET Library](https://github.com/j-brooke/FracturedJson/wiki/.NET-Library) - available from [NuGet](https://www.nuget.org/packages/FracturedJson).
* [fracjson CLI app](https://github.com/j-brooke/FracturedJson/wiki/fracjson-CLI) - installable from [NuGet](https://www.nuget.org/packages/fracjson) as a .NET global tool, or downloadable from the [Releases](https://github.com/j-brooke/FracturedJson/releases) page as self-contained executables.
* The [browser-based formatter](https://j-brooke.github.io/FracturedJson/) - hosted by GitHub Pages.

For more information or to see other implementations, see the [project wiki](https://github.com/j-brooke/FracturedJson/wiki).
