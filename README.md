# Codex

An extensible platform for indexing and exploring code inspired by [Source Browser](https://github.com/KirillOsenkov/SourceBrowser). This powers https://ref12.io.

Create and host your own static website to browse your C#/VB/MSBuild source code using WebAssembly.

Of course Codex allows you to browse its own source code:
http://ref12.github.io/Codex

## Building (in Powershell)
1. git clone https://github.com/Ref12/Codex
2. cd Codex
3. ```./build.ps1``` (can also call `dotnet build Codex.xln` direct;u)

## Instructions to generate and run a website

1. Generate wasm binaries

    ` dotnet publish src/Codex.Web.Wasm/Codex.Web.Wasm.csproj -o bin/wasm `

2. Generate Codex executable

    ` dotnet publish src/Codex.Application/Codex.Application.csproj -o bin/exe `

3. Deploy the wasm binaries

    ` ./deploywasm.ps1 ` - deploys static site to `bin/web` folder by default

4. Build repo and generate binlog: ` ./build.ps1 `

5. Analyze repo and binlogs to produce analysis outputs: ` ./analyze.ps1 `

6. Create or update index ` ./ingest.ps1 -OutputDir bin/web/index`

7. Test locally using ` dotnet serve `

    `dotnet serve -p 58226 -S --directory bin/web`

## Features
* Semantic analysis of C#, VB, and MSBuild
* Syntax coloring for C#, VB, MSBuild, XML
* Go To Definition (click on a reference)
* Find All References (click on a definition)
* Project Explorer - in any document click on the Project link at the bottom
* Document Outline - for a document click on the button in top right to display types and members in the current file
* Clicking on the partial keyword will display a list of all files where this type is declared
* MSBuild files (.csproj etc) have hyperlinks

## Conceptual design

Indexing happens in two phases: Analysis and Ingestion. During analysis, C#/VB source files are analyzed using Roslyn and files are generated containing the extracted semantic information. These files are typically packaged into a zip file and ingested elsewhere. During ingestion, the analysis files are loaded an their information is added to a custom Lucene database. The database is used to satisfy all queries. The database format allows it to be queried in a serverless manner using WASM but this is untenable for larger codebases or aggregations of codebases so https://ref12.io uses a server based approach.

Indexing *is* incremental. Many repos can be analyzed and ingested at different times.

### Limitations and known issues
 * Analysis requires a successful build of projects or successful extraction of compilation using Roslyn MSBuildWorkspace.

## License

This project is licensed with the [MIT license](LICENSE).
