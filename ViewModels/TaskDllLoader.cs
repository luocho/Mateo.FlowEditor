using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;

namespace FlowEditor.ViewModels
{
    /// <summary>
    /// 插件隔离加载器：每次加载都新建一个可回收的 AssemblyLoadContext，
    /// 解决反复加载/重编译同一 DLL 时默认上下文中的程序集标识冲突与文件占用问题。
    /// 主程序集通过字节流加载，不占用 DLL 文件句柄，加载完即可释放上下文。
    /// </summary>
    public sealed class TaskDllLoader : IDisposable
    {
        private readonly AssemblyLoadContext _context;
        private readonly AssemblyDependencyResolver _resolver;

        public TaskDllLoader(string dllPath)
        {
            string fullPath = Path.GetFullPath(dllPath);
            _context = new AssemblyLoadContext($"FlowEditor.TaskDll.{Guid.NewGuid():N}", isCollectible: true);
            _resolver = new AssemblyDependencyResolver(fullPath);
            _context.Resolving += OnResolving;
        }

        /// <summary>返回 DLL 中实现 ITask 接口（名字不区分大小写）的类全名，按字母排序</summary>
        public IReadOnlyList<string> GetITaskTypeNames(string dllPath)
        {
            string fullPath = Path.GetFullPath(dllPath);
            var asm = _context.LoadFromStream(new MemoryStream(File.ReadAllBytes(fullPath)));

            Type[] types;
            try
            {
                types = asm.GetTypes();
            }
            catch (ReflectionTypeLoadException ex)
            {
                types = ex.Types.Where(t => t != null).Cast<Type>().ToArray();
            }

            return types
                .Where(t => t.IsClass && !t.IsAbstract && t.IsVisible && !t.IsGenericTypeDefinition)
                .Where(t => t.GetInterfaces().Any(
                    i => string.Equals(i.Name, "ITask", StringComparison.OrdinalIgnoreCase)))
                .Select(t => t.FullName ?? t.Name)
                .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        public void Dispose()
        {
            _context.Resolving -= OnResolving;
            _context.Unload();
        }

        /// <summary>插件依赖（如共享的 ITask 所在程序集）优先从插件目录解析</summary>
        private Assembly? OnResolving(AssemblyLoadContext context, AssemblyName name)
        {
            string? path = _resolver.ResolveAssemblyToPath(name);
            return path != null ? context.LoadFromAssemblyPath(path) : null;
        }
    }
}
