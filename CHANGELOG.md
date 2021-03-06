# FracturedJson Change Log

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
