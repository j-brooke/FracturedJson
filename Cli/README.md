# fracjson CLI

**fracjson** is a command line tool for formatting JSON data so that it's fairly compact but highly readable.  It uses the [FracturedJson](https://github.com/j-brooke/FracturedJson) .NET library.  It's available as a **dotnet global tool** for people who use the .NET ecosystem, and as a **standalone executable** for major platforms for people who don't want to install .NET.

## Installing

To install as a .NET tool, type this:
```
dotnet tool install --global fracjson
```

You'll need a .NET runtime installed on your machine to use it, of course.

If you're rather not use .NET, you can download one of the self-contained executables from the [FracturedJson Releases](https://github.com/j-brooke/FracturedJson/releases) page.  You'll have to decide where to put the file on your own and make sure it's set as an executable with `chmod` or similar.  You might also want to put it in your `$PATH`.

## Usage

Use a file or `stdin` to specify the input file.  The output will be sent to your terminal, unless you pipe it somewhere.  Alternately, you can use the `--output-file` switch (or `-o`) to write it directly to a file.

```
fracjson rawFile.json -o formattedFile.json
```

```
echo '{"a":3,"b":false}' | fracjson > out.json
```

The default settings work pretty well with most JSON data, but there are lots of options available to fine-tune the output for your data.  You can specify those options on the command line or through a config file.  See [the fracjson wiki page](https://github.com/j-brooke/FracturedJson/wiki/fracjson-CLI) for more information.

---
[Project Wiki](https://github.com/j-brooke/FracturedJson/wiki) — [Formatting Options](https://github.com/j-brooke/FracturedJson/wiki/Options) — [Web Formatter](https://j-brooke.github.io/FracturedJson/) — [Implementations & Ports](https://github.com/j-brooke/FracturedJson/wiki/Available-Versions-and-Ports)
