#region Copyright & License Information
/*
 * Copyright 2007-2015 The OpenRA Developers (see AUTHORS)
 * This file is part of OpenRA, which is free software. It is made
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation. For more information,
 * see COPYING.
 */
#endregion

using System.Collections.Generic;
using OpenRA.Support;
using OpenRA.Traits;

namespace OpenRA.Mods.Common.Traits
{
	/// <summary>
	/// A decorator used to cache the pathfinder (Decorator design pattern)
	/// </summary>
	public class PathFinderCacheDecorator : IPathFinder
	{
		readonly IPathFinder pathFinder;
		readonly ICacheStorage<List<CPos>> cacheStorage;

		public PathFinderCacheDecorator(IPathFinder pathFinder, ICacheStorage<List<CPos>> cacheStorage)
		{
			this.pathFinder = pathFinder;
			this.cacheStorage = cacheStorage;
		}

		public List<CPos> FindUnitPath(CPos from, CPos target, IActor self)
		{
			using (new PerfSample("Pathfinder"))
			{
				var key = "FindUnitPath" + self.ActorID + from.X + from.Y + target.X + target.Y;
				var cachedPath = cacheStorage.Retrieve(key);

				if (cachedPath != null)
					return cachedPath;

				var pb = pathFinder.FindUnitPath(from, target, self);

				cacheStorage.Store(key, pb);

				return pb;
			}
		}

		public List<CPos> FindUnitPathToRange(CPos src, SubCell srcSub, WPos target, WRange range, IActor self)
		{
			using (new PerfSample("Pathfinder"))
			{
				var key = "FindUnitPathToRange" + self.ActorID + src.X + src.Y + target.X + target.Y;
				var cachedPath = cacheStorage.Retrieve(key);

				if (cachedPath != null)
					return cachedPath;

				var pb = pathFinder.FindUnitPathToRange(src, srcSub, target, range, self);

				cacheStorage.Store(key, pb);

				return pb;
			}
		}

		public List<CPos> FindPath(IPathSearch search)
		{
			using (new PerfSample("Pathfinder"))
			{
				var key = "FindPath" + search.Id;
				var cachedPath = cacheStorage.Retrieve(key);

				if (cachedPath != null)
					return cachedPath;

				var pb = pathFinder.FindPath(search);

				cacheStorage.Store(key, pb);

				return pb;
			}
		}

		public List<CPos> FindBidiPath(IPathSearch fromSrc, IPathSearch fromDest)
		{
			using (new PerfSample("Pathfinder"))
			{
				var key = "FindBidiPath" + fromSrc.Id + fromDest.Id;
				var cachedPath = cacheStorage.Retrieve(key);

				if (cachedPath != null)
					return cachedPath;

				var pb = pathFinder.FindBidiPath(fromSrc, fromDest);

				cacheStorage.Store(key, pb);

				return pb;
			}
		}
	}
}
