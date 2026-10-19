# Coding standards

These are the rules the coding standards check in Lab 18 applies to every pull request that changes C# code. The check turns each rule into a question for Microsoft-Decision-1: does the pull request pass, or does it break the rule? Each rule has an ID, so the check can report which one a change breaks, and a decision for when it's broken: `block` fails the check, and `warn` adds a warning but lets it pass.

The check reads this file, so keep the format: each rule is a `## ID: Title` heading, the rule's text, and a last line that starts with `Decision when broken:` and names `block` or `warn`.

## CS1: The library doesn't write to the console

Code in `src/WorkshopCore` doesn't call `Console.Write`, `Console.WriteLine`, or `Console.Error`. It's a library that other projects call, so it returns values or throws exceptions, and the caller decides what to show. Code in `src/WorkshopDashboard` and in tests may write to the console.

Decision when broken: `block`.

## CS2: Catch only exceptions you can handle

Don't catch `Exception` or `SystemException`, and don't write an empty `catch` block. Catch the specific exceptions the code can recover from, such as `FormatException` or `IOException`, and let everything else reach the caller. A `catch` that logs the exception and rethrows it with `throw;` is fine.

Decision when broken: `block`.

## CS3: No secrets in code

Don't put passwords, API keys, tokens, or connection strings with credentials in code, including in constants, test data, and comments. Read them from configuration or environment variables instead. Placeholder values that are obviously not real, such as `YOUR-API-KEY`, are fine.

Decision when broken: `block`.

## CS4: Asynchronous methods end in Async

A method that returns `Task`, `Task<T>`, `ValueTask`, or `ValueTask<T>` has a name that ends in `Async`, such as `SaveTotalAsync`. Test methods and `Main` are exceptions.

Decision when broken: `warn`.

## CS5: Private fields start with an underscore

Private instance fields use camel case with a leading underscore, such as `_results`. Constants and static readonly fields use Pascal case, such as `MaxResults`.

Decision when broken: `warn`.

## CS6: New public methods come with tests

A pull request that adds a public method to `src/WorkshopCore` also adds a test for it in `tests/WorkshopCore.Tests`, in the same pull request.

Decision when broken: `warn`.
