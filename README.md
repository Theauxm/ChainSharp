# ChainSharp

This repository is meant to serve as a way to implement the Chain method commonly used in Functional Programming.
Also commonly known as Railway Programming, the project consists of a class called `Workflow`. A `Workflow` consists of a series of Steps, each called a `Step`.  Each `Step` can either return a `Left` or a `Right`. the former implies an error has occured, and the latter implies the `Step` was successful.  If at any point a `Left` is returned, the `Workflow` will track the error, and throw it at the end.

This is beneficial over traditional error handling as it provides the user with granularity in *where* the error was generated. The `Workflow` encapsulates the Exception, rather than putting the responsibility of error handling on each `Step`.