# FracturedJson

FracturedJson is a library for formatting JSON documents in a human-readable but fairly compact way.  There's also a .NET Core 3.1 commandline app.

## Example

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

## Discussion

Most JSON libraries give you a choice between two formatting options.  Minified JSON is very efficient, but difficult for a person to read.

```json
{"AttackPlans":[{"TeamId":1,"Spawns":[{"Time":0.0,"UnitType":"Grunt","SpawnPointIndex":0},{"Time":0.0,"UnitType":"Grunt","SpawnPointIndex":0},{"Time":0.0,"UnitType":"Grunt","SpawnPointIndex":0}]}],"DefensePlans":[{"TeamId":2,"Placements":[{"UnitType":"Archer","Position":[41,7]},{"UnitType":"Pikeman","Position":[40,7]},{"UnitType":"Barricade","Position":[39,7]}]}]}
```

Most beautified/indented JSON, on the other hand, is too spread out, often making it difficult to read as well.

```json
{
    "AttackPlans": [
        {
            "TeamId": 1,
            "Spawns": [
                {
                    "Time": 0,
                    "UnitType": "Grunt",
                    "SpawnPointIndex": 0
                },
                {
                    "Time": 0,
                    "UnitType": "Grunt",
                    "SpawnPointIndex": 0
                },
                {
                    "Time": 0,
                    "UnitType": "Grunt",
                    "SpawnPointIndex": 0
                }
            ]
        }
    ],
    "DefensePlans": [
        {
            "TeamId": 2,
            "Placements": [
                {
                    "UnitType": "Archer",
                    "Position": [
                        41,
                        7
                    ]
                },
                {
                    "UnitType": "Pikeman",
                    "Position": [
                        40,
                        7
                    ]
                },
                {
                    "UnitType": "Barricade",
                    "Position": [
                        39,
                        7
                    ]
                }
            ]
        }
    ]
}
```

FracturedJson tries to format data like a person would.  Complex elements are kept to a single line as long as they're not too complex, and not too long.

```json
{
    "AttackPlans": [
        {
            "TeamId": 1,
            "Spawns": [
                {"Time": 0.0, "UnitType": "Grunt", "SpawnPointIndex": 0},
                {"Time": 0.0, "UnitType": "Grunt", "SpawnPointIndex": 0},
                {"Time": 0.0, "UnitType": "Grunt", "SpawnPointIndex": 0}
            ]
        }
    ],
    "DefensePlans": [
        {
            "TeamId": 2,
            "Placements": [
                { "UnitType": "Archer", "Position": [41, 7] },
                { "UnitType": "Pikeman", "Position": [40, 7] },
                { "UnitType": "Barricade", "Position": [39, 7] }
            ]
        }
    ]
}
```
