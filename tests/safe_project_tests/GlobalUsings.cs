// Absorbs the Ihc.Vis.* sub-namespace fan-out introduced by the vis reorganization, so individual
// test files need no per-file using for each level namespace (Model/Projects/Products/FunctionBlocks/
// Catalog/Editing/Io/Schema/Validation) plus the Ihc.Vis facade.
global using Ihc.Vis;
global using Ihc.Vis.Model;
global using Ihc.Vis.Projects;
global using Ihc.Vis.Products;
global using Ihc.Vis.FunctionBlocks;
global using Ihc.Vis.Catalog;
global using Ihc.Vis.Editing;
global using Ihc.Vis.Io;
global using Ihc.Vis.Reporting;
global using Ihc.Vis.Schema;
global using Ihc.Vis.Validation;
// The schema TypeCode helper collides with System.TypeCode once Ihc.Vis.Schema and System are both
// imported; alias it project-wide to the schema type the vis tests always mean.
global using TypeCode = Ihc.Vis.Schema.TypeCode;
