# ProjectGmKernel.Xt

Parasolid text XT parsing, lossless document handling, and schema-node-typed
models for .NET 10.

V30-V38 use built-in compiled descriptors and generated node/field types. Older
and future versions use caller-provided schemas through
`XtSchemaCatalog.OpenDirectory`. This package contains no `.sch_txt` source,
Parasolid API/header, kernel, or licensed runtime, and never downloads schemas.
