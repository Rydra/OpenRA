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
using Moq;
using OpenRA.Mods.Common.Traits;
using OpenRA.Support;
using OpenRA.Traits;

namespace OpenRA.Test
{
	public static class TestUtils
	{
		public static Mock<IActor> GenerateActor(IMobile mobile, IWorld world, IMobileInfo mobileInfo)
		{
			var actor = new Mock<IActor>();
			actor.Setup(x => x.Trait<IMobile>()).Returns(mobile);
			actor.SetupGet(x => x.World).Returns(world);
			actor.SetupGet(x => x.Location).Returns(() => mobile.ToCell);
			actor.Setup(x => x.TraitInfo<IMobileInfo>()).Returns(mobileInfo);
			return actor;
		}

		public static Mock<IWorld> GenerateWorld(IActor worldActor, IActorMap actorMap, IMap map)
		{
			var world = new Mock<IWorld>();
			world.SetupGet(x => x.WorldActor).Returns(worldActor);
			world.Setup(x => x.ActorMap).Returns(actorMap);
			world.SetupGet(x => x.Map).Returns(map);
			world.SetupGet(x => x.SharedRandom).Returns(new MersenneTwister());
			return world;
		}

		public static Mock<IMap> GenerateMapMock()
		{
			var map = new Mock<IMap>();
			map.Setup(x => x.CenterOfSubCell(It.IsAny<CPos>(), It.IsAny<SubCell>()))
				.Returns((CPos c, SubCell s) => CenterOfSubCell(c, s));
			map.Setup(x => x.OffsetOfSubCell(It.IsAny<SubCell>())).Returns((SubCell s) => OffsetOfSubCell(s));
			map.Setup(x => x.CenterOfCell(It.IsAny<CPos>())).Returns((CPos c) => CenterOfCell(c));
			return map;
		}

		public static Mock<IPathFinder> GeneratePathfinderTraitMock()
		{
			return new Mock<IPathFinder>();
		}

		public static Mock<IActor> GenerateWorldActorMock(IPathFinder pathFinder)
		{
			var worldActor = new Mock<IActor>();
			worldActor.Setup(x => x.Trait<IPathFinder>()).Returns(pathFinder);
			return worldActor;
		}

		public static Mock<IActorMap> GenerateActorMapMock()
		{
			var actorMap = new Mock<IActorMap>();
			actorMap.Setup(x => x.GetActorsAt(It.IsAny<CPos>())).Returns(new List<IActor>());
			return actorMap;
		}

		public static Mock<IMobileInfo> GenerateMobileInfoMock()
		{
			var mobileInfo = new Mock<IMobileInfo>();
			mobileInfo.SetupAllProperties();
			return mobileInfo;
		}

		static WVec OffsetOfSubCell(SubCell subCell) { return subCellOffsets[(int)subCell]; }

		static WVec[] subCellOffsets = new WVec[]
									{
										new WVec(0, 0, 0),
										new WVec(-299, -256, 0),
										new WVec(256, -256, 0),
										new WVec(0, 0, 0),
										new WVec(-299, 256, 0),
										new WVec(256, 256, 0)
									};

		static WPos CenterOfCell(CPos cell)
		{
			return new WPos(1024 * cell.X + 512, 1024 * cell.Y + 512, 0);
		}

		static WPos CenterOfSubCell(CPos cell, SubCell subCell)
		{
			var index = (int)subCell;
			if (index >= 0 && index <= subCellOffsets.Length)
				return CenterOfCell(cell) + subCellOffsets[index];
			return CenterOfCell(cell);
		}
	}
}
