namespace DevkitServer.Models;

public delegate void ForEach<in T>(T value);
public delegate bool ForEachWhile<in T>(T value);