# FracturedJson Change Log

## 3.1.0

### Added

* New setting: `OmitTrailingWhitespace`.  When true, the output JSON won't have any trailing spaces or tabs.  This is probably the preferred behavior in most cases, but the default is `false` for backward compatibility.

## 3.0.2

### Bug Fixes

* Fixed a [bug](https://github.com/j-brooke/FracturedJson/issues/20) where the AlwaysExpandDepth setting was preventing table formatting. 

## 3.0.1

### Bug Fixes

* Fixed a problem where top level comments weren't being ignored when the settings said they should.
* Fixed a problem where more than one top-level element (actual data, not comments) would be processed.

## 3.0.0

### Features

* Support for comments (sometimes called JSON-with-comments or .jsonc).  Where possible, comments stay stuck to the elements that they're closest to in the input.
* Deep table formatting.  In version 2, only the immediate children of table rows were lined up.  Now, if space permits and the types are consistent, all descendents are aligned as table columns.
* New length limit option: `MaxTotalLineLength`.
* Option to preserve blank lines.
* Option to allow trailing commas.

### Added

* New settings: `MaxTotalLineLength`, `MaxTableRowComplexity`, `MinCompactArrayRowItems`, `CommentPolicy`, `PreserveBlankLines`, `AllowTrailingCommas`.

### Removed

* Removed settings: `TableObjectMinimumSimilarity`, `TableArrayMinimumSimilarity`, `AlignExpandedPropertyNames`, `JsonSerializerOptions`.
* Support for East Asian Full-width characters is no longer built-in.  I did this to eliminate coupling with any specific library.  You can easily recreate the functionality by providing your own `StringLengthFunc`.  (See the `EastAsianWideCharactersTests` test class for an example.)

### Changed

* All of the settings are now bundled in a single class, `FracturedJsonOptions`.  They are now set all at once to `Formatter.Options` rather than being separate properties of `Formatter`.
* Method names have changed.  Use `Reformat` when you're providing a JSON text, or `Serialize<T>` when providing .NET objects.


## 2.2.1

### Bug Fixes

* Fixed a [bug](https://github.com/j-brooke/FracturedJson/issues/16) caused by numbers with superfluous digits when `DontJustifyNumbers` is `true`.

## 2.2.0

### Added

* New property `StringWidthFunc` determines how many spaces are used as padding to line up columns when formatted as a table.
    * Static method `Formatter.StringWidthWithEastAsian` (default) uses two spaces for East Asian "fullwidth" symbols, and one space for others.
    * Static method `Formatter.StringWidthByCharacterCount` treats each character as having the width of one space.
* New property `SimpleBracketPadding` controls whether brackets should have spaces inside when they contain only simple elements.  (The old property `NestedBracketPadding` is used when they contain other arrays/objects.)

## 2.0.1

### Bug Fixes

* Escape sequences in property names are not preserved (#3)

## 2.0.0

Re-written to support table-formatting.  When an expanded array or object is composed of highly similar inline arrays or objects, FracturedJson tries to format them in a tabular format, sorting properties and justifying values to make everything line up neatly.

### Added

* New properties `IndentSpaces` and `UseTabToIndent` to control indentation.
* New properties `TableObjectMinimumSimilarity` and `TableArrayMinimumSimilarity` control how alike inline sibling elements need to be to be formatted as a table.
* New property `AlignExpandedPropertyNames` to line up expanded object property names even when not treated as a table.
* New property `DontJustifyNumbers` prevents numbers from being right-justified and set to matching precision.
* New property `PrefixString` allows adding arbitrary characters at the start of each line.
* Commandline flags `--array`, `--object`, `--no-justify` have been added corresponding to the new Formatter properties.


### Removed

* `JustifyNumberLists` property has been removed.  The new table formatting covers this functionality better.
* `IndentString` property has been removed.  `IndentSpaces` and `UseTabToIndent` are used to control indentation instead.  The flexibility intended for `IndentString` turned out not to be worth the confusion.
* Commandline flag `--justify` has been removed.


### Changed

* Formatter property `JsonSerializerOptions` replaces the `options` parameter of `Serialize<T>`.
