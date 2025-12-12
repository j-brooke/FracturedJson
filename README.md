# FracturedJson

FracturedJson is a family of utilities that format [JSON data](https://www.json.org) in a way that's easy for
humans to read, but fairly compact.  Arrays and objects are written on single lines, as long as they're
neither too long nor too complex.  When several such lines are similar in structure, they're written with
fields aligned like a table.  Long arrays are written with multiple items per line across multiple lines.

It is available as a browser page, a .NET Standard 2.0 library, a JavaScript package, and a Visual Studio Code 
extension.

Here's a sample of output using nearly default settings. (`MaxTotalLineLength=100`)
```json
{
  "SimpleArray"           : [
    2,   3,   5,   7,  11,  13,  17,  19,  23,  29,  31,  37,  41,  43,  47,  53,  59,  61,
    67,  71,  73,  79,  83,  89,  97, 101, 103, 107, 109, 113
  ],
  "ObjectColumnsArrayRows": {
    "Katherine": ["blue",       "lightblue", "black"       ],
    "Logan"    : ["yellow",     "blue",      "black", "red"],
    "Erik"     : ["red",        "purple"                   ],
    "Jean"     : ["lightgreen", "yellow",    "black"       ]
  },
  "ArrayColumnsObjectRows": [
    { "type": "turret",    "hp": 400, "loc": {"x": 47, "y":  -4}, "flags": "S"   },
    { "type": "assassin",  "hp":  80, "loc": {"x": 12, "y":   6}, "flags": "Q"   },
    { "type": "berserker", "hp": 150, "loc": {"x":  0, "y":   0}                 },
    { "type": "pittrap",              "loc": {"x": 10, "y": -14}, "flags": "S,I" }
  ],
  "ComplexArray"          : [
    [19,  2], [ 3,  8], [14,  0], [ 9,  9], [ 9,  9], [ 0,  3], [10,  1], [ 9,  1], [ 9,  2],
    [ 6, 13], [18,  5], [ 4, 11], [12,  2]
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

## Getting Started

To get started using the .NET library, see the 
[.NET library wiki page](https://github.com/j-brooke/FracturedJson/wiki/.NET-Library).

## More Information

Please see the [project wiki](https://github.com/j-brooke/FracturedJson/wiki) for more information on what you can do and how to use the tools.  Or, visit 
the [browser-based formatter](https://j-brooke.github.io/FracturedJson/) to experiment.
