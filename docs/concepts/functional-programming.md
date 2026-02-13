---
layout: default
title: Functional Programming
parent: Core Concepts
nav_order: 1
---

# Functional Programming

ChainSharp borrows a few ideas from functional programming. You don't need an FP background to use it, but knowing where these types come from makes the API click faster.

## LanguageExt

ChainSharp depends on [LanguageExt](https://github.com/louthy/language-ext), a functional programming library for C#. You'll interact with two of its types: `Either` and `Unit`.

## Either\<L, R\>

`Either<L, R>` represents a value that is one of two things: `Left` or `Right`. By convention, `Left` is the failure case and `Right` is the success case.

ChainSharp uses `Either<Exception, T>` as the return type for workflows. A workflow either fails with an exception or succeeds with a result:

```csharp
protected override async Task<Either<Exception, User>> RunInternal(CreateUserRequest input)
    => Activate(input)
        .Chain<ValidateEmailStep>()
        .Chain<CreateUserStep>()
        .Resolve();
```

You'll see `Either<Exception, T>` in every `RunInternal` signature. The chain handles the wrapping—if a step throws, the chain catches it and returns `Left(exception)`. If everything succeeds, you get `Right(result)`.

To inspect the result:

```csharp
var result = await workflow.Run(input);

result.Match(
    Left: exception => Console.WriteLine($"Failed: {exception.Message}"),
    Right: user => Console.WriteLine($"Created: {user.Email}"));

// Or check directly
if (result.IsRight)
{
    var user = (User)result;
}
```

This is the foundation of [Railway Oriented Programming](railway-programming.md)—the success track carries `Right` values, the failure track carries `Left` values.

## Unit

`Unit` means "no meaningful return value." It's the functional equivalent of `void`, except you can use it as a generic type argument.

In C#, you can't write `Task<void>` or `Step<string, void>`. `Unit` fills that gap:

```csharp
public class ValidateEmailStep : Step<CreateUserRequest, Unit>
{
    public override async Task<Unit> Run(CreateUserRequest input)
    {
        if (!IsValidEmail(input.Email))
            throw new ValidationException("Invalid email");

        return Unit.Default;
    }
}
```

`Unit.Default` is the only value of the `Unit` type. When a step returns `Unit`, it's saying "I did my work, but I'm not producing a new value for [Memory](memory.md)." The next step in the chain pulls its input from whatever's already available.
