﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using BepInEx.Configuration;
using BepInEx.Logging;
using Mono.Cecil;

namespace BepInEx.Bootstrap
{
	public abstract class BaseChainloader<TPlugin>
	{
		#region Contract

		protected virtual string ConsoleTitle => $"BepInEx {typeof(Paths).Assembly.GetName().Version} - {Paths.ProcessName}";

		private bool _initialized = false;

		/// <summary>
		/// List of all <see cref="PluginInfo"/> instances loaded via the chainloader.
		/// </summary>
		public Dictionary<string, PluginInfo> Plugins { get; } = new Dictionary<string, PluginInfo>();

		/// <summary>
		/// Collection of error chainloader messages that occured during plugin loading.
		/// Contains information about what certain plugins were not loaded.
		/// </summary>
		public List<string> DependencyErrors { get; } = new List<string>();

		public virtual void Initialize(string gameExePath = null)
		{
			if (_initialized)
				throw new InvalidOperationException("Chainloader cannot be initialized multiple times");

			// Set vitals
			if (gameExePath != null)
			{
				// Checking for null allows a more advanced initialization workflow, where the Paths class has been initialized before calling Chainloader.Initialize
				// This is used by Preloader to use environment variables, for example
				Paths.SetExecutablePath(gameExePath);
			}

			InitializeLoggers();

			if (!Directory.Exists(Paths.PluginPath))
				Directory.CreateDirectory(Paths.PluginPath);

			if (!Directory.Exists(Paths.PatcherPluginPath))
				Directory.CreateDirectory(Paths.PatcherPluginPath);

			_initialized = true;

			Logger.LogMessage("Chainloader initialized");
		}

		protected virtual void InitializeLoggers()
		{
			if (ConsoleManager.ConfigConsoleEnabled.Value && !ConsoleManager.ConsoleActive)
				ConsoleManager.CreateConsole();

			if (ConsoleManager.ConsoleActive)
			{
				if (!Logger.Listeners.Any(x => x is ConsoleLogListener))
					Logger.Listeners.Add(new ConsoleLogListener());

				ConsoleManager.SetConsoleTitle(ConsoleTitle);
			}

			if (ConfigDiskLogging.Value)
				Logger.Listeners.Add(new DiskLogListener("LogOutput.log", ConfigDiskLoggingDisplayedLevel.Value, ConfigDiskAppend.Value, ConfigDiskLoggingInstantFlushing.Value, ConfigDiskLoggingFileLimit.Value));

			if (!TraceLogSource.IsListening)
				Logger.Sources.Add(TraceLogSource.CreateSource());

			if (!Logger.Sources.Any(x => x is HarmonyLogSource))
				Logger.Sources.Add(new HarmonyLogSource());
		}

		protected virtual IList<PluginInfo> DiscoverPlugins()
		{
			var pluginsToLoad = TypeLoader.FindPluginTypes(Paths.PluginPath, ToPluginInfo, HasBepinPlugins, "chainloader");

			return pluginsToLoad.SelectMany(p => p.Value).ToList();
		}

		protected virtual IList<PluginInfo> ModifyLoadOrder(IList<PluginInfo> plugins)
		{
			// We use a sorted dictionary to ensure consistent load order
			var dependencyDict = new SortedDictionary<string, IEnumerable<string>>(StringComparer.InvariantCultureIgnoreCase);
			var pluginsByGuid = new Dictionary<string, PluginInfo>();

			foreach (var pluginInfoGroup in plugins.GroupBy(info => info.Metadata.GUID))
			{
				PluginInfo loadedVersion = null;
				foreach (var pluginInfo in pluginInfoGroup.OrderByDescending(x => x.Metadata.Version))
				{
					if (loadedVersion != null)
					{
						Logger.LogWarning($"Skipping [{pluginInfo}] because a newer version exists ({loadedVersion})");
						continue;
					}

					// Perform checks that will prevent loading plugins in this run
					var filters = pluginInfo.Processes.ToList();
					bool invalidProcessName = filters.Count != 0 && filters.All(x => !string.Equals(x.ProcessName.Replace(".exe", ""), Paths.ProcessName, StringComparison.InvariantCultureIgnoreCase));

					if (invalidProcessName)
					{
						Logger.LogWarning($"Skipping [{pluginInfo}] because of process filters ({string.Join(", ", pluginInfo.Processes.Select(p => p.ProcessName).ToArray())})");
						continue;
					}

					loadedVersion = pluginInfo;
					dependencyDict[pluginInfo.Metadata.GUID] = pluginInfo.Dependencies.Select(d => d.DependencyGUID);
					pluginsByGuid[pluginInfo.Metadata.GUID] = pluginInfo;
				}
			}

			foreach (var pluginInfo in pluginsByGuid.Values.ToList())
			{
				if (pluginInfo.Incompatibilities.Any(incompatibility => pluginsByGuid.ContainsKey(incompatibility.IncompatibilityGUID)))
				{
					pluginsByGuid.Remove(pluginInfo.Metadata.GUID);
					dependencyDict.Remove(pluginInfo.Metadata.GUID);

					var incompatiblePlugins = pluginInfo.Incompatibilities.Select(x => x.IncompatibilityGUID).Where(x => pluginsByGuid.ContainsKey(x)).ToArray();
					string message = $@"Could not load [{pluginInfo}] because it is incompatible with: {string.Join(", ", incompatiblePlugins)}";
					DependencyErrors.Add(message);
					Logger.LogError(message);
				}
				else if (PluginTargetsWrongBepin(pluginInfo))
				{
					string message = $@"Plugin [{pluginInfo}] targets a wrong version of BepInEx ({pluginInfo.TargettedBepInExVersion}) and might not work until you update";
					DependencyErrors.Add(message);
					Logger.LogWarning(message);
				}
			}

			var emptyDependencies = new string[0];

			// Sort plugins by their dependencies.
			// Give missing dependencies no dependencies of its own, which will cause missing plugins to be first in the resulting list.
			var sortedPlugins = Utility.TopologicalSort(dependencyDict.Keys, x => dependencyDict.TryGetValue(x, out var deps) ? deps : emptyDependencies).ToList();

			return sortedPlugins.Where(pluginsByGuid.ContainsKey).Select(x => pluginsByGuid[x]).ToList();
		}

		public virtual void Execute()
		{
			try
			{
				var plugins = DiscoverPlugins();

				Logger.LogInfo($"{plugins.Count} plugin{(plugins.Count == 1 ? "" : "s")} to load");

				var sortedPlugins = ModifyLoadOrder(plugins);

				var invalidPlugins = new HashSet<string>();
				var processedPlugins = new Dictionary<string, Version>();
				var loadedAssemblies = new Dictionary<string, Assembly>();

				foreach (var plugin in sortedPlugins)
				{
					var dependsOnInvalidPlugin = false;
					var missingDependencies = new List<BepInDependency>();
					foreach (var dependency in plugin.Dependencies)
					{
						bool IsHardDependency(BepInDependency dep)
							=> (dep.Flags & BepInDependency.DependencyFlags.HardDependency) != 0;

						// If the dependency wasn't already processed, it's missing altogether
						bool dependencyExists = processedPlugins.TryGetValue(dependency.DependencyGUID, out var pluginVersion);
						if (!dependencyExists || pluginVersion < dependency.MinimumVersion)
						{
							// If the dependency is hard, collect it into a list to show
							if (IsHardDependency(dependency))
								missingDependencies.Add(dependency);
							continue;
						}

						// If the dependency is a hard and is invalid (e.g. has missing dependencies), report that to the user
						if (invalidPlugins.Contains(dependency.DependencyGUID) && IsHardDependency(dependency))
						{
							dependsOnInvalidPlugin = true;
							break;
						}
					}

					processedPlugins.Add(plugin.Metadata.GUID, plugin.Metadata.Version);

					if (dependsOnInvalidPlugin)
					{
						string message = $"Skipping [{plugin}] because it has a dependency that was not loaded. See previous errors for details.";
						DependencyErrors.Add(message);
						Logger.LogWarning(message);
						continue;
					}

					if (missingDependencies.Count != 0)
					{
						bool IsEmptyVersion(Version v) => v.Major == 0 && v.Minor == 0 && v.Build <= 0 && v.Revision <= 0;

						string message = $@"Could not load [{plugin}] because it has missing dependencies: {
								string.Join(", ", missingDependencies.Select(s => IsEmptyVersion(s.MinimumVersion) ? s.DependencyGUID : $"{s.DependencyGUID} (v{s.MinimumVersion} or newer)").ToArray())
							}";
						DependencyErrors.Add(message);
						Logger.LogError(message);

						invalidPlugins.Add(plugin.Metadata.GUID);
						continue;
					}

					try
					{
						Logger.LogInfo($"Loading [{plugin}]");

						if (!loadedAssemblies.TryGetValue(plugin.Location, out var ass))
							loadedAssemblies[plugin.Location] = ass = Assembly.LoadFile(plugin.Location);

						Plugins[plugin.Metadata.GUID] = plugin;
						plugin.Instance = LoadPlugin(plugin, ass);

						//_plugins.Add((TPlugin)plugin.Instance);
					}
					catch (Exception ex)
					{
						invalidPlugins.Add(plugin.Metadata.GUID);
						Plugins.Remove(plugin.Metadata.GUID);

						Logger.LogError($"Error loading [{plugin}] : {ex.Message}");
						if (ex is ReflectionTypeLoadException re)
							Logger.LogDebug(TypeLoader.TypeLoadExceptionToString(re));
						else
							Logger.LogDebug(ex);
					}
				}
			}
			catch (Exception ex)
			{
				try
				{
					ConsoleManager.CreateConsole();
				}
				catch { }

				Logger.LogError("Error occurred starting the game");
				Logger.LogDebug(ex);
			}

			Logger.LogMessage("Chainloader startup complete");
		}

		public abstract TPlugin LoadPlugin(PluginInfo pluginInfo, Assembly pluginAssembly);

		#endregion

		private static Regex allowedGuidRegex { get; } = new Regex(@"^[a-zA-Z0-9\._\-]+$");

		/// <summary>
		/// Analyzes the given type definition and attempts to convert it to a valid <see cref="PluginInfo"/>
		/// </summary>
		/// <param name="type">Type definition to analyze.</param>
		/// <param name="assemblyLocation">The filepath of the assembly, to keep as metadata.</param>
		/// <returns>If the type represent a valid plugin, returns a <see cref="PluginInfo"/> instance. Otherwise, return null.</returns>
		public static PluginInfo ToPluginInfo(TypeDefinition type, string assemblyLocation)
		{
			if (type.IsInterface || type.IsAbstract)
				return null;

			try
			{
				if (!type.IsSubtypeOf(typeof(TPlugin)))
					return null;
			}
			catch (AssemblyResolutionException)
			{
				// Can happen if this type inherits a type from an assembly that can't be found. Safe to assume it's not a plugin.
				return null;
			}

			var metadata = BepInPlugin.FromCecilType(type);

			// Perform checks that will prevent the plugin from being loaded in ALL cases
			if (metadata == null)
			{
				Logger.LogWarning($"Skipping over type [{type.FullName}] as no metadata attribute is specified");
				return null;
			}

			if (string.IsNullOrEmpty(metadata.GUID) || !allowedGuidRegex.IsMatch(metadata.GUID))
			{
				Logger.LogWarning($"Skipping type [{type.FullName}] because its GUID [{metadata.GUID}] is of an illegal format.");
				return null;
			}

			if (metadata.Version == null)
			{
				Logger.LogWarning($"Skipping type [{type.FullName}] because its version is invalid.");
				return null;
			}

			if (metadata.Name == null)
			{
				Logger.LogWarning($"Skipping type [{type.FullName}] because its name is null.");
				return null;
			}

			var filters = BepInProcess.FromCecilType(type);
			var dependencies = BepInDependency.FromCecilType(type);
			var incompatibilities = BepInIncompatibility.FromCecilType(type);

			var bepinVersion = type.Module.AssemblyReferences.FirstOrDefault(reference => reference.Name == "BepInEx.Core")?.Version ?? new Version();

			return new PluginInfo
			{
				Metadata = metadata,
				Processes = filters,
				Dependencies = dependencies,
				Incompatibilities = incompatibilities,
				TypeName = type.FullName,
				TargettedBepInExVersion = bepinVersion,
				Location = assemblyLocation
			};
		}

		protected static readonly string CurrentAssemblyName = Assembly.GetExecutingAssembly().GetName().Name;
		protected static readonly Version CurrentAssemblyVersion = Assembly.GetExecutingAssembly().GetName().Version;

		protected static bool HasBepinPlugins(AssemblyDefinition ass)
		{
			if (ass.MainModule.AssemblyReferences.All(r => r.Name != CurrentAssemblyName))
				return false;
			if (ass.MainModule.GetTypeReferences().All(r => r.FullName != typeof(TPlugin).FullName))
				return false;

			return true;
		}

		protected static bool PluginTargetsWrongBepin(PluginInfo pluginInfo)
		{
			var pluginTarget = pluginInfo.TargettedBepInExVersion;
			// X.X.X.x - compare normally. x.x.x.X - nightly build number, ignore
			if (pluginTarget.Major != CurrentAssemblyVersion.Major) return true;
			if (pluginTarget.Minor > CurrentAssemblyVersion.Minor) return true;
			if (pluginTarget.Minor < CurrentAssemblyVersion.Minor) return false;
			return pluginTarget.Build > CurrentAssemblyVersion.Build;
		}

		#region Config

		private static readonly ConfigEntry<bool> ConfigDiskAppend = ConfigFile.CoreConfig.Bind(
			"Logging.Disk", "AppendLog",
			false,
			"Appends to the log file instead of overwriting, on game startup.");

		private static readonly ConfigEntry<bool> ConfigDiskLogging = ConfigFile.CoreConfig.Bind(
			"Logging.Disk", "Enabled",
			true,
			"Enables writing log messages to disk.");

		private static readonly ConfigEntry<LogLevel> ConfigDiskLoggingDisplayedLevel = ConfigFile.CoreConfig.Bind(
			"Logging.Disk", "LogLevels",
			LogLevel.Fatal | LogLevel.Error | LogLevel.Warning | LogLevel.Message | LogLevel.Info,
			"Only displays the specified log levels in the disk log output.");

		private static readonly ConfigEntry<bool> ConfigDiskLoggingInstantFlushing = ConfigFile.CoreConfig.Bind(
			"Logging.Disk", "InstantFlushing",
			false,
			new StringBuilder()
				.AppendLine("If true, instantly writes any received log entries to disk.")
				.AppendLine("This incurs a major performance hit if a lot of log messages are being written, however it is really useful for debugging crashes.")
				.ToString());

		private static readonly ConfigEntry<int> ConfigDiskLoggingFileLimit = ConfigFile.CoreConfig.Bind(
			"Logging.Disk", "ConcurrentFileLimit",
			5,
			new StringBuilder()
				.AppendLine("The maximum amount of concurrent log files that will be written to disk.")
				.AppendLine("As one log file is used per open game instance, you may find it necessary to increase this limit when debugging multiple instances at the same time.")
				.ToString());

		#endregion
	}
}
