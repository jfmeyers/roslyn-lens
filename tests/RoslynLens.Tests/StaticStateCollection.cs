namespace RoslynLens.Tests;

/// <summary>
/// Tests that mutate WorkspaceInitializer static state must not run in parallel.
/// </summary>
[CollectionDefinition("StaticState", DisableParallelization = true)]
public class StaticStateCollection;
