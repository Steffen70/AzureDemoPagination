﻿using System.Reflection;
using Microsoft.AspNetCore.Mvc;
using System.Text;
using System.Text.Json;
using SPPaginationDemo.Filtration;

#pragma warning disable IDE0290

namespace SPPaginationDemo.Controllers;

public class FileInfoModel
{
    public string Name { get; set; } = null!;
    public string Path { get; set; } = null!;
    public bool IsDirectory { get; set; }
    public List<FileInfoModel> Children { get; set; } = new();
}

[ApiController]
[Route("server-call")]
public class LogicController : Controller
{
    private readonly IWebHostEnvironment _env;
    private readonly ILogger _logger;

    public LogicController(IWebHostEnvironment env, ILogger logger)
    {
        _env = env;
        _logger = logger;
    }

    [HttpGet("files-structure")]
    public ActionResult<FileInfoModel> GetFilesStructure()
    {
        try
        {
            var rootDirectory = _env.ContentRootPath;
            return Ok(GetDirectoryStructure(rootDirectory));
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Internal server error: '{ex.Message}'");
        }
    }


    private static FileInfoModel GetDirectoryStructure(string rootPath)
    {
        var directoryInfo = new DirectoryInfo(rootPath);
        var root = new FileInfoModel
        {
            Name = directoryInfo.Name,
            Path = directoryInfo.FullName,
            IsDirectory = true
        };

        foreach (var dir in directoryInfo.GetDirectories()) 
            root.Children.Add(GetDirectoryStructure(dir.FullName));

        foreach (var file in directoryInfo.GetFiles())
            root.Children.Add(new FileInfoModel
            {
                Name = file.Name,
                Path = file.FullName,
                IsDirectory = false
            });

        return root;
    }


    [HttpPost("callback/{typeName}/{methodName}")]
    public async Task<ActionResult> ServerCall(string typeName, string methodName)
    {
        using var reader = new StreamReader(Request.Body, Encoding.UTF8);

        var json = await reader.ReadToEndAsync();

        // deserialize Params from json
        var parameters = JsonSerializer.Deserialize<Dictionary<string, object>>(json, HttpExtensions.Options);

        // create instance of type by typeName
        // Todo: DS: Add dependency injection pipeline
        var type = GetType(typeName);
        var instance = Activator.CreateInstance(type);

        // get method by methodName
        var method = type.GetMethod(methodName);

        // if method is null return bad request with error message
        if (method == null)
            return BadRequest($"Method '{methodName}' not found");

        var result = parameters == null ? method.Invoke(instance, null) : method.Invoke(instance, parameters.Values.ToArray());

#pragma warning disable CA2254
        _logger.LogInformation($"Method '{methodName}' of type '{typeName}' was called");
#pragma warning restore CA2254
        return Ok(result);
    }

    private static readonly List<Type> CachedTypes = new();
    private static Type GetType(string typeName)
    {
        var type = CachedTypes.FirstOrDefault(c => c.FullName == typeName);

        if (type != null)
            return type;

        type = Type.GetType(typeName);

        if (type != null)
            return type;

        var namespaceString = typeName[..typeName.LastIndexOf('.')];
        var assembly = Assembly.Load(namespaceString);

        // get Type from assembly by typeName

        type = assembly.GetType(typeName);

        if (type == null) throw new Exception($"Type {typeName} not found");

        CachedTypes.Add(type);
        return type;
    }
}