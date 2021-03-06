﻿using System;
using System.IO;
using System.Threading;
using DotnetSpider.Core.Redial.Redialer;
using System.Linq;
using DotnetSpider.Core.Redial.InternetDetector;
using System.Collections.Concurrent;
using DotnetSpider.Core.Infrastructure;
using LogLevel = NLog.LogLevel;

namespace DotnetSpider.Core.Redial
{
	public class FileLockerRedialExecutor : RedialExecutor
	{
		private static readonly string AtomicActionFolder;
		private static readonly string RedialLockerFile;
		private static readonly string RedialTimeFile;
		private static readonly ConcurrentDictionary<string, Stream> Files = new ConcurrentDictionary<string, Stream>();

		static FileLockerRedialExecutor()
		{
			AtomicActionFolder = Path.Combine(Env.GlobalDirectory, "atomicaction");
			RedialLockerFile = Path.Combine(Env.GlobalDirectory, "redial.lock");
			RedialTimeFile = Path.Combine(Env.GlobalDirectory, "redial.time");
		}

		public FileLockerRedialExecutor(IRedialer redialer, IInternetDetector validater) : base(redialer, validater)
		{
			if (!Directory.Exists(AtomicActionFolder))
			{
				Directory.CreateDirectory(AtomicActionFolder);
			}

			foreach (var file in Directory.GetFiles(AtomicActionFolder))
			{
				try
				{
					File.Delete(file);
				}
				catch
				{
				}
			}
			try
			{
				File.Delete(RedialLockerFile);
			}
			catch
			{
			}
		}

		public override void WaitAllNetworkRequestComplete()
		{
			while (true)
			{
				if (!Directory.GetFiles(AtomicActionFolder).Any())
				{
					break;
				}
				Thread.Sleep(50);
			}
		}

		public override string CreateActionIdentity(string name)
		{
			string id = Path.Combine(AtomicActionFolder, name + "-" + Guid.NewGuid().ToString("N"));
			Files.TryAdd(id, File.Create(id));
			return id;
		}

		public override void DeleteActionIdentity(string identity)
		{
			Files.TryRemove(identity, out var stream);
			stream?.Dispose();
			File.Delete(identity);
		}

		public override void LockRedial()
		{
			while (File.Exists(RedialLockerFile))
			{
				Thread.Sleep(50);
				continue;
			}
			Files.AddOrUpdate(RedialLockerFile, File.Create(RedialLockerFile));
		}

		public override void ReleaseRedialLock()
		{
			Files.TryRemove(RedialLockerFile, out var stream);
			stream?.Dispose();
			File.Delete(RedialLockerFile);
		}

		public override bool IsRedialing()
		{
			return File.Exists(RedialLockerFile);
		}

		public override DateTime GetLastRedialTime()
		{
			if (File.Exists(RedialTimeFile))
			{
				return DateTime.Parse(File.ReadAllText(RedialTimeFile).Trim());
			}
			else
			{
				return new DateTime();
			}
		}

		public override void RecordLastRedialTime()
		{
			File.AppendAllText(RedialTimeFile, DateTime.Now.ToString());
		}
	}
}
