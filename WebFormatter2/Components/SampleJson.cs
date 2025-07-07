namespace WebFormatter2.Components;

public static class SampleJson
{
    public const string PureJson =
        """
        {"SimpleArray":[2,3,5,7,11,13,17,19,23,29,31,37,41,43,47,53,59,61,67,71,73,79,83,89,97,101,103,107,109,113],
        "ObjectColumnsArrayRows":{"Katherine":["blue","lightblue","black"],"Logan":["yellow","blue","black","red"],
        "Erik":["red","purple"],"Jean":["lightgreen","yellow","black"]},"ArrayColumnsObjectRows":[{"type":"turret",
        "hp":400,"loc":{"x":47,"y":-4},"flags":"S"},{"type":"assassin","hp":80,"loc":{"x":12,"y":6},"flags":"Q"},
        {"type":"berserker","hp":150,"loc":{"x":0,"y":0}},{"type":"pittrap","loc":{"x":10,"y":-14},"flags":"S,I"}],
        "ComplexArray":[[19,2],[3,8],[14,0],[9,9],[9,9],[0,3],[10,1],[9,1],[9,2],[6,13],[18,5],[4,11],[12,2]]}
        """;

    public const string JsonWithComments =
        """
        {
        /*
         * Multi-line comments
         * are fun!
         */
        "NumbersWithHex":[254/*00FE*/,1450/*5AA*/,0/*0000*/,36000/*8CA0*/,10/*000A*/,199/*00C7*/,15001/*3A99*/,
        6540/*198C*/]
        /* Elements are keen */
        ,"Elements":[{/*Carbon*/"Symbol":"C","Number":6,"Isotopes":[11,12,13,14]},{/*Oxygen*/"Symbol":"O","Number":8,
        "Isotopes":[16,18,17]},{/*Hydrogen*/"Symbol":"H","Number":1,"Isotopes":[1,2,3]},{/*Iron*/"Symbol":"Fe",
        "Number":26,"Isotopes":[56,54,57,58]}
        // Not a complete list...
        ]

        ,"Beatles Songs":["Taxman"// George
        ,"Hey Jude"// Paul
        ,"Act Naturally"// Ringo
        ,"Ticket To Ride"// John
        ]}
        """;
}