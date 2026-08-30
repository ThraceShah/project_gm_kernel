# ProjectGmKernel.Xt

Schema-agnostic Parasolid text XT parsing, lossless document handling, and a
data-oriented B-rep interchange model for .NET 10.

This package contains no Parasolid schema, descriptor, field table, kernel,
header, or licensed runtime. Callers must legally obtain their own text schema
files and explicitly pass their directory to `XtSchemaCatalog.OpenDirectory`.
The package never downloads schemas and has no built-in fallback.
