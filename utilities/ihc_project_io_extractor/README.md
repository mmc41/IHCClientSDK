# IHC IO Extractor programming utility.

This project contains an optional command line utility that can generate constant definitions of IO addresses in a concrete IHC installation
for use by software developers targeting specific installations.

The project works with a project file as input and output code files for the installation.

Use this approach in your projects if you don't want to lookup and hardcode IO addresses yourself.

The project currently supports *.cs, *.js, *.ts and *.json output but it is easy to add support for more languages by updating the `Generators.cs` file.




