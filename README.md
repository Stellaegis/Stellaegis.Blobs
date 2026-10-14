# Stellaegis.Blobs

Stellaegis.Blobs is a small .NET library for storing opaque binary content: files, documents,
templates, snapshots. Your code reads and writes blobs through one interface, `IBlobStore`, and
addresses each blob with a `BlobLocation`. The storage backend sits behind the interface and is
chosen by the adapter you register, so your code and the keys it stores do not depend on it.

Two packages are published:

| Package | Content |
| --- | --- |
| `Stellaegis.Blobs` | `IBlobStore` and `BlobLocation` |
| `Stellaegis.Blobs.FileSystem` | an adapter that stores blobs as files under a root directory |

The library targets .NET 10 and is released under the MIT license.

```sh
dotnet add package Stellaegis.Blobs.FileSystem
```

The adapter package depends on `Stellaegis.Blobs`, so this one command installs both.

## Addressing a blob: compartment, space and key

A `BlobLocation` has three parts.

- **The compartment** is the access boundary the blob belongs to: a customer, an organization, a
  tenant, whatever your application partitions its data by. It is a `Compartment`, a type from
  [Stellaegis.Primitives](https://github.com/Stellaegis/Stellaegis.Primitives) that your application
  creates from its own identifiers.
- **The space** (`Name`) groups blobs of one kind: `invoices`, `templates`, `archive`. The library
  defines no space; your application chooses its own names.
- **The key** is the path of the blob within its space, such as `invoice.pdf` or
  `2026/06/08/3f2a.pdf`. Segments are separated by forward slashes.

```csharp
var acme = Compartment.Parse("acme");
var invoice = new BlobLocation(acme, "invoices", "2026/06/08/3f2a.pdf");
```

Each compartment and space pair is isolated from the others: the same key under two compartments
designates two different blobs.

### Content that belongs to no compartment: the shared space

Some content belongs to the application as a whole rather than to one compartment: a default
template, a document shared by every customer. It lives in the shared space, reached through
`BlobLocation.Shared`:

```csharp
var template = BlobLocation.Shared("templates", "invoice.scriban");

template.IsShared;    // true
template.Compartment; // null
```

The shared space is a case of its own, not a compartment with a reserved name. No `Compartment`
value can designate it, whatever name your application gives its compartments, and each adapter
stores it under a physical name that no compartment can produce. A compartment named `shared` is an
ordinary compartment, distinct from the shared space.

The constructor refuses a `null` compartment with an `ArgumentNullException`, instead of reading it
as the shared space. A compartment that your code failed to resolve, such as a missing route value,
therefore fails loudly rather than reaching shared content. Reaching the shared space always takes an
explicit call to `BlobLocation.Shared`.

The alternative would be a compartment name reserved by convention, such as `platform`. It needs no
special case in the library, but it holds only as long as every part of your application refuses
that name as a real compartment. The shared space holds without that discipline, at the cost of a
nullable `Compartment` property.

If your application has a single compartment, name it once and use it everywhere:

```csharp
static readonly Compartment Main = Compartment.Parse("main");
```

A location holds no physical information: no provider, endpoint, container or credential. That
information belongs to the adapter. As a result, a stored key stays valid when the backend changes,
and it never carries a secret.

### What a location accepts

The constructor checks each part and throws an `ArgumentException` naming the part at fault when
one is invalid.

| Part | Rule |
| --- | --- |
| compartment | any `Compartment`, never `null`: 3 to 63 characters, a lowercase letter first, then lowercase letters, digits and single hyphens, ending with a letter or a digit. `Compartment.Parse` and `Compartment.TryParse` apply this rule |
| space | 1 to 63 characters: a lowercase letter, then lowercase letters, digits and hyphens |
| key | see below |

A key is made of segments separated by `/`. It is accepted when every supported backend can store it
unchanged, on Windows and Linux file systems as well as in object storages such as S3 or Azure Blob:

- it uses lowercase printable ASCII only: lowercase letters, digits, spaces and punctuation;
- it contains none of `< > : " | ? * \`;
- no segment is empty, `.` or `..`, and no segment ends with a dot or a space;
- no segment is a name Windows reserves for a device: `con`, `prn`, `aux`, `nul`, `com0` to `com9`,
  `lpt0` to `lpt9`, with or without an extension (`nul.txt` is refused);
- it is at most 1,024 characters long (`BlobLocation.MaxKeyLength`), and each segment at most 255
  (`BlobLocation.MaxSegmentLength`).

Each rule closes a case where at least one backend would refuse the key, change it, or confuse it
with another. On Windows, `a:b` reads as a path on drive `A:`, a trailing dot or space is silently
dropped, so `report.` and `report` are the same file, and `nul.txt` opens a device rather than a file.
Windows and macOS file systems ignore case, so `Report.pdf` and `report.pdf` would be one file there
and two blobs elsewhere. Unicode letters can be stored in two forms, composed or decomposed, which two
different keys can share once displayed. Refusing these keys up front means a key that works with one
adapter works with all of them.

These rules make keys technical identifiers, built by your code: `2026/06/08/3f2a.pdf`,
`3f2a9c/v1`. A name chosen by a user, such as `Mon fichier avec des accents.pdf`, keeps its case,
accents and spaces as metadata stored beside the blob, typically in your database, and is given back
at download time, for instance in the `Content-Disposition` header.

The compartment and space rules keep every pair on a distinct directory or container name, and the
key rules keep a key inside its space (no path traversal). None of these rules is a business rule. If
your application constrains its space names further, check that rule in your code before building a
location. The library has no way to know it.

## Reading and writing

`IBlobStore` has four methods:

| Method | Behaviour |
| --- | --- |
| `WriteAsync(location, content)` | writes the stream, replacing any blob already at that location |
| `OpenReadAsync(location)` | returns a readable stream, or `null` when no blob exists at that location |
| `ExistsAsync(location)` | tells whether a blob exists at that location |
| `DeleteAsync(location)` | deletes the blob; does nothing when there is none |

```csharp
await store.WriteAsync(invoice, pdfStream, cancellationToken);

await using var content = await store.OpenReadAsync(invoice, cancellationToken);
if (content is null)
{
    // no blob at this location
}
```

An absent blob and an empty blob are two different cases. An absent blob reads as `null`. An empty
blob, written from a stream of length 0, reads as a stream of length 0. A document with no content
is therefore stored and found again like any other.

`OpenReadAsync` returns `null` rather than throwing because absence is an ordinary answer for a
store. Your code decides what an absent blob means: a missing optional file, or an inconsistency
with metadata that says the blob exists. Errors that are not about absence, such as a denied access
or a full disk, are raised as exceptions by the adapter.

### Serving content to a user

The library stores and reads content; it does not serve it. To let a user download a blob, your
application reads it with `OpenReadAsync` and writes it to the response itself, with the file name and
content type it keeps as metadata. Every download therefore goes through your application, which
checks access on each request and can record who downloaded what.

The alternative is a pre-signed URL that sends the browser straight to the storage backend. It saves
your application from relaying the bytes, but access is then granted by a signature and a lifetime
rather than checked on each request, and a leaked URL stays valid until it expires. The library does
not offer it. The bytes reach the browser in both cases, so relaying them costs your application's
time and bandwidth, not extra transfer to the user.

## The filesystem adapter

`FileSystemBlobStore` stores each blob as a file under a root directory, with one directory per
compartment, and one per space inside it:

```text
{root}/acme/invoices/2026/06/08/3f2a.pdf      compartment "acme", space "invoices"
{root}/_shared/templates/invoice.scriban      the shared space, space "templates"
```

Everything a compartment holds sits under its own directory, so you can back it up, move it to
another storage or erase it in one step. The shared space sits under `_shared`. A compartment starts
with a letter, so no compartment can produce that directory.

Register the adapter with `AddStellaegisFileSystemBlobStore`. It registers `FileSystemBlobStore` as
the singleton `IBlobStore`; a second call keeps the first registration.

```csharp
builder.Services.AddStellaegisFileSystemBlobStore(o => o.RootPath = "/var/lib/myapp/blobs");
```

To read the root from configuration, bind the section named by `FileSystemBlobOptions.SectionName`:

```csharp
builder.Services.AddStellaegisFileSystemBlobStore(
    o => builder.Configuration.GetSection(FileSystemBlobOptions.SectionName).Bind(o));
```

```json
{
  "Stellaegis": {
    "Blobs": {
      "FileSystem": { "RootPath": "/var/lib/myapp/blobs" }
    }
  }
}
```

`RootPath` is required: the store throws an `ArgumentException` when it is created without one. A
relative path is resolved against the process's current directory when the store is created.
Directories are created on the first write and are not removed when their last blob is deleted.

The adapter suits development, tests and applications that run on a single machine. The stream
`OpenReadAsync` returns is a file stream, so it supports seeking, which serving a byte range
requires. The key rules above already keep out what Windows or Linux would refuse. The remaining
limit is the total path length: the root directory, the compartment and space directories and the
key add up, and a file system or a tool that still
caps paths at 260 characters on Windows can refuse a long one. Choose a short root if your keys are
long.

## One storage per compartment

Because a location carries the compartment and nothing physical, an `IBlobStore` can send each
compartment to a storage of its own. This is how a customer can bring its own storage account: your
implementation of `IBlobStore` looks up the storage configured for `location.Compartment`, then
delegates to an adapter created for that storage. A shared location has no compartment, so your
implementation sends it to a storage of your choice. The keys your application stores do not change
when a compartment moves from one storage to another; only the content has to be copied.

The library does not provide this routing. It depends on where your application keeps each
compartment's storage settings and credentials, and on how long it keeps an adapter per compartment,
all of which your application decides.

## Building and testing

```sh
dotnet test --solution Stellaegis.Blobs.slnx
```

The tests run on Microsoft.Testing.Platform, which `global.json` selects for `dotnet test`. That
mode needs the .NET 10 SDK and the `--solution` option.

The tests need no external service. The filesystem tests write under the system's temporary
directory and remove what they write.

## License

MIT — see [LICENSE](https://github.com/Stellaegis/Stellaegis.Blobs/blob/main/LICENSE). The packages depend on third-party components listed in
[THIRD-PARTY-NOTICES.md](https://github.com/Stellaegis/Stellaegis.Blobs/blob/main/THIRD-PARTY-NOTICES.md).
