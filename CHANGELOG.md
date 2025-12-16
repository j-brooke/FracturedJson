# FracturedJson Change Log

## 5.0.0

### Features

Properties of expanded objects (ones that aren't inlined or table-formatted) are now aligned so that their values begin in the same column.  This makes for a more consistent look between table-formatted and expanded objects.  The new setting `MaxPropNamePadding` controls how much space FracturedJson is allowed to add, since it looks bad and wastes space if some property names are much longer than others.  (Set to `-1` to disable this feature.  Use `ColonBeforePropNamePadding` to put colons next to the property names instead of in a line of their own.)

Example:
```json
{
  "ModuleId"         : "armor",
  "Name"             : "",
  "FreeformLocations": [
    [11,  2], [11,  3], [11,  4], [11,  5], [11,  6], [11,  7], [11,  8], [11,  9],
    [11, 10], [11, 11], [11, 12], [11, 13], [11, 14], [ 1, 14], [ 1, 13], [ 1, 12],
    [ 1, 11], [ 1, 10], [ 1,  9], [ 1,  8], [ 1,  7], [ 1,  6], [ 1,  5], [ 1,  4],
    [ 1,  3], [ 1,  2], [ 4,  2], [ 5,  2], [ 6,  2], [ 7,  2], [ 8,  2], [ 8,  3],
    [ 7,  3], [ 6,  3], [ 5,  3], [ 4,  3], [ 0,  4], [ 0,  5], [ 0,  6], [ 0,  7],
    [ 0,  8], [12,  8], [12,  7], [12,  6], [12,  5], [12,  4]
  ],
  "Orientation"      : "Fore",
  "Seed"             : 272691529
}
```

### Settings changes

* New: `MaxPropNamePadding` - limits expanded property alignment when property names differ greatly in length.  (Doesn't apply to table formatting.)
* New: `ColonBeforePropNamePadding` - controls where property name padding goes relative to the colon.
* Removed: `OmitTrailingWhitespace` - Trailing whitespace is always removed now.
* Removed: `MaxInlineLength` - Stopped being useful once `MaxTotalLineLength` was introduced a few versions ago.
* Default changed: `MaxCompactArrayComplexity` - now defaults to `2` instead of `1`.
* Default changed: `NumberListAlignment` - now defaults to `Decimal` instead of `Normalize`.  There are many cases where changing numbers' representations can alter how software treats them.  `Decimal` always preserves exactly how numbers were written in their source documents, so it's a safer default.
* Default changed: `TableCommaPlacement` - now defaults to `BeforePaddingExceptNumbers` instead of `AfterPadding`.  It looks nicer when `NumberListAlignment=Decimal`.

## 4.1.1

### Bug Fix

* Fixed a [bug](https://github.com/j-brooke/FracturedJson/issues/44) causing an exception when a table column contains both nulls and very short non-empty arrays.

## 4.1.0

### Features

Added a new option for where commas are placed with table formatting.  The old (and still default) behavior puts commas after the column padding.  This causes all the commas for a column to line up, which sometimes looks silly, especially when it's just a single column of strings being formatted as a table.

Now you can set `TableCommaPlacement` to `TableCommaPlacement.BeforePadding` to make the commas cling tightly to the values to their left.  Or, if you prefer, you can use `TableCommaPlacement.BeforePaddingExceptNumbers` to leave your number columns unaffected.  (That option is only meaningful when `NumberListAlignment` is `Left` or `Decimal`.)

Also added was a new factory method, `FracturedJsonOptions.Recommended()` which, unlike the `FracturedJsonOptions` constructor, will always point to the new best defaults without regard for backward compatibility.

### Added

* New setting `FracturedJsonOptions.TableCommaPlacement`.
* New method `FracturedJsonOptions.Recommended()`.

### Build Environment

Updated the CLI and Tests projects to target .NET 8.  The FracturedJson library itself is still .NET Standard 2.0 compliant.

## 4.0.3

### Package Updates

* Updated to using System.Text.Json version 6.0.10 to address a [vulnerability](https://github.com/advisories/GHSA-8g4q-xg66-9fp4).

## 4.0.2

### Bug Fixes

* Fixed a [bug](https://github.com/j-brooke/FracturedJson/issues/33) where an exception is thrown if the current locale uses a number format that's incompatible with JSON's number representation.  (E.g., a locale that uses "," instead of "." as the decimal separator.)

## 4.0.1

### Bug Fixes

* Fixed a [bug](https://github.com/j-brooke/FracturedJson/issues/32) where no exception is thrown when there's a property name but no value at the end of an object.

## 4.0.0

### Features

Replaced setting `DontJustifyNumbers` with a new enum, `NumberListAlignment`, to control how arrays or table columns of numbers are handled.

* `Normalize` is the default and it behaves like previous versions when `DontJustifyNumbers==false`.  With it, number lists or columns are rewritten with the same number of digits after the decimal place.
* `Decimal` lines the numbers up according to their decimal points, preserving the number exactly as it appeared in the input document.  For regular numbers this usually looks like `Normalize`, except with spaces padding out the extra decimal places instead of zeros.
* `Left` lines up the values on the left, preserving the exactly values from the input document.
* `Right` lines up the values on the right, preserving the exactly values from the input document.

### Added

New setting, `NumberListAlignment`.

### Removed

Removed setting `DontJustifyNumbers`.

## 3.1.1

* Fixed a [bug](https://github.com/j-brooke/FracturedJson/issues/27) where numbers that overflow or underflow a 64-bit float could (depending on settings) be written to the output as `Infinity` or `0`.  In the overflow case, that caused output to be invalid JSON.  With this fix, FracturedJson recognizes that it can't safely reform numbers like this, and uses the exact number representation from the original document.

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
* Deep table formatting.  In version 2, only the immediate children of table rows were lined up.  Now, if space permits and the types are consistent, all descendants are aligned as table columns.
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
