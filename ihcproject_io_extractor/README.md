# IHC IO Extractor

This project contains an optional command line utility that can generate constant definitions of IO addresses in a concrete IHC installation. 

Use this approach in your projects if you don't want to lookup and hardcode IO addresses yourself.

The project currently supports *.cs, *.js, *.ts and *.json output but it is easy to add support for more languages by updating the `Generators.cs` file.

**IMPORTANT**: Please create a ihcsettings.json in the root dir of the repo
before running this tool. Refer to the ihcsettings_template.json
for required format.



