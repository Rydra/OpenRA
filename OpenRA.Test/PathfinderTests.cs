#region Copyright & License Information
/*
 * Copyright 2007-2015 The OpenRA Developers (see AUTHORS)
 * This file is part of OpenRA, which is free software. It is made
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation. For more information,
 * see COPYING.
 */
#endregion

using System;
using System.Collections.Generic;
using System.Diagnostics;
using Moq;
using NUnit.Framework;
using OpenRA;
using OpenRA.Mods.Common.Pathfinder;
using OpenRA.Mods.Common.Traits;

namespace PathfinderTests
{
	[TestFixture]
	public class PathfinderTests
	{
		const int Width = 128;
		const int Height = 128;
		Mock<IWorld> world;
		Mock<IMap> map;
		Mock<IActor> actor;
		Mock<IMobileInfo> mobileInfo;

		[SetUp]
		public void Setup()
		{
			map = GenerateMap(Width, Height);
			world = GenerateWorld(map.Object);
			mobileInfo = GenerateMobileInfoMock();
			actor = GenerateActor(new Mock<IMobile>().Object, world.Object, mobileInfo.Object);
		}

		private static Mock<IMobileInfo> GenerateMobileInfoMock()
		{
			var mobileInfo = new Mock<IMobileInfo>();
			mobileInfo.SetupAllProperties();
			return mobileInfo;
		}

		static Mock<IMap> GenerateMap(int mapWidth, int mapHeight)
		{
			var map = new Mock<IMap>();
			map.SetupGet(m => m.TileShape).Returns(TileShape.Rectangle);
			map.Setup(m => m.MapSize).Returns(new int2(mapWidth, mapHeight));
			map.Setup(m => m.Contains(It.Is<CPos>(pos => IsValidPos(pos, mapWidth, mapHeight)))).Returns(true);

			return map;
		}

		static Mock<IWorld> GenerateWorld(IMap map)
		{
			var world = new Mock<IWorld>();
			world.SetupGet(m => m.Map).Returns(map);
			world.SetupGet(m => m.WorldActor).Returns(new Mock<IActor>().Object);
			return world;
		}

		static Mock<IActor> GenerateActor(IMobile mobile, IWorld world, IMobileInfo mobileInfo)
		{
			var actor = new Mock<IActor>();
			actor.Setup(x => x.Trait<IMobile>()).Returns(mobile);
			actor.SetupGet(x => x.World).Returns(world);
			actor.SetupGet(x => x.Location).Returns(() => mobile.ToCell);
			actor.Setup(x => x.TraitInfo<IMobileInfo>()).Returns(mobileInfo);
			return actor;
		}

		static bool IsValidPos(CPos pos, int mapWidth, int mapHeight)
		{
			return pos.X >= 0 && pos.X < mapWidth && pos.Y >= 0 && pos.Y < mapHeight;
		}

		static IEnumerable<CPos[]> TestData()
		{
			yield return new[] { new CPos(1, 1), new CPos(125, 74), new CPos(50, 100) };
			yield return new[] { new CPos(0, 0), new CPos(51, 100), new CPos(50, 100) };
			yield return new[] { new CPos(0, 0), new CPos(49, 50), new CPos(49, 50) };
			yield return new[] { new CPos(127, 0), new CPos(50, 101), new CPos(50, 101) };
		}

		[TestCaseSource("TestData")]
		public void FindPathOnRoughTerrainTest(CPos source, CPos target, CPos expected)
		{
			// Arrange
			// Create the MobileInfo Mock. Playing with this can help to
			// check the different paths and points a unit can walk into
			int dummy = 125;
			mobileInfo.Setup(
				x =>
				x.CanEnterCell(
					world.Object,
					actor.Object,
					It.Is<CPos>(pos => !(!IsValidPos(pos, Width, Height) ||
					(pos.X == 50 && pos.Y < 100) ||
					(pos.X == 100 && pos.Y > 50))),
					out dummy,
					It.IsAny<IActor>(),
					It.IsAny<CellConditions>())).Returns(true);

			int dummy2 = int.MaxValue;
			mobileInfo.Setup(
				x =>
				x.CanEnterCell(
					world.Object,
					actor.Object,
					It.Is<CPos>(pos => !IsValidPos(pos, Width, Height) ||
					(pos.X == 50 && pos.Y < 100) ||
					(pos.X == 100 && pos.Y > 50)),
					out dummy2,
					It.IsAny<IActor>(),
					It.IsAny<CellConditions>())).Returns(false);

			var pathfinder = new PathFinder(world.Object);

			// Act
			var search = PathSearch.FromPoint(world.Object, mobileInfo.Object, actor.Object, source, target, true);
			var path = pathfinder.FindPath(search);
			Assert.Contains(expected, path);
		}

		[Test]
		[Ignore("Not implemented yet")]
		public void PathNotFoundTest()
		{
			// Arrange
			// Create the MobileInfo Mock. Playing with this can help to
			// check the different paths and points a unit can walk into
			int dummy = 125;
			mobileInfo.Setup(
				x =>
				x.CanEnterCell(
					world.Object,
					actor.Object,
					It.Is<CPos>(pos => !(!IsValidPos(pos, Width, Height) ||
					(pos.X == 50))),
					out dummy,
					It.IsAny<IActor>(),
					It.IsAny<CellConditions>())).Returns(true);

			int dummy2 = int.MaxValue;
			mobileInfo.Setup(
				x =>
				x.CanEnterCell(
					world.Object,
					actor.Object,
					It.Is<CPos>(pos => !IsValidPos(pos, Width, Height) ||
					(pos.X == 50)),
					out dummy2,
					It.IsAny<IActor>(),
					It.IsAny<CellConditions>())).Returns(false);

			var pathfinder = new PathFinder(world.Object);

			// Act
			var search = PathSearch.FromPoint(world.Object, mobileInfo.Object, actor.Object, new CPos(1, 1), new CPos(125, 75), true);
			var path = pathfinder.FindPath(search);
			Assert.Contains(new CPos(50, 100), path);
		}

		static int Est1(CPos here, CPos destination)
		{
			var diag = Math.Min(Math.Abs(here.X - destination.X), Math.Abs(here.Y - destination.Y));
			var straight = Math.Abs(here.X - destination.X) + Math.Abs(here.Y - destination.Y);

			// Min cost to arrive from once cell to an adjacent one
			// (125 according to tests)
			const int D = 100;

			// According to the information link, this is the shape of the function.
			// We just extract factors to simplify.
			var h = D * straight + (D * Constants.Sqrt2 - 2 * D) * diag;

			return (int)(h * 1.001);
		}

		static int Est2(CPos here, CPos destination)
		{
			var diag = Math.Min(Math.Abs(here.X - destination.X), Math.Abs(here.Y - destination.Y));
			var straight = Math.Abs(here.X - destination.X) + Math.Abs(here.Y - destination.Y);

			// HACK: this relies on fp and cell-size assumptions.
			var h = (100 * diag * Constants.Sqrt2) + 100 * (straight - (2 * diag));
			return (int)(h * 1.001);
		}

		/// <summary>
		/// Tests the refactor of the default heuristic for pathFinder
		/// </summary>
		[Test]
		public void EstimatorsTest()
		{
			Assert.AreEqual(Est1(new CPos(0, 0), new CPos(20, 30)), Est2(new CPos(0, 0), new CPos(20, 30)));
		}

		[Test]
		public void Remove1000StoredPaths()
		{
			var world = new Mock<IWorld>();
			world.SetupGet(m => m.WorldTick).Returns(50);
			var pathCacheStorage = new PathCacheStorage(world.Object);
			var stopwatch = new Stopwatch();
			for (var i = 0; i < 1100; i++)
			{
				if (i == 1000)
				{
					// Let's make the world tick further so we can trigger the removals
					// when storing more stuff
					world.SetupGet(m => m.WorldTick).Returns(110);
					stopwatch.Start();
				}

				pathCacheStorage.Store(i.ToString(), new List<CPos>());
				if (i == 1000)
				{
					stopwatch.Stop();
					Console.WriteLine("I took " + stopwatch.ElapsedMilliseconds + " ms to remove 1000 stored paths");
				}
			}
		}

		static IEnumerable<object[]> RayCastingTestData()
		{
			yield return new object[] { new CPos(1, 3), new CPos(3, 0),
				new[]
					{
						new CPos(1, 3), new CPos(1, 2), new CPos(2, 2), new CPos(2, 1),
   						new CPos(3, 1), new CPos(3, 0)
					}
			};
		}

		/// <summary>
		/// Test for the future feature of path smoothing for Pathfinder
		/// </summary>
		[TestCaseSource("RayCastingTestData")]
		public void RayCastingTest(CPos source, CPos target, IEnumerable<CPos> expectedCuts)
		{
			// Arrange
			var sut = new RayCaster();

			// Act
			var cutCells = sut.RayCast(source, target);

			// Assert
			CollectionAssert.AreEqual(expectedCuts, cutCells);
		}
	}

	public class RayCaster
	{
		// Algorithm obtained in http://playtechs.blogspot.co.uk/2007/03/raytracing-on-grid.html
		public IEnumerable<CPos> RayCast(CPos source, CPos target)
		{
			var dx = Math.Abs(target.X - source.X);
			var dy = Math.Abs(target.Y - source.Y);
			var x = source.X;
			var y = source.Y;
			var n = 1 + dx + dy;
			var x_inc = (target.X > source.X) ? 1 : -1;
			var y_inc = (target.Y > source.Y) ? 1 : -1;
			var error = dx - dy;
			dx *= 2;
			dy *= 2;

			for (; n > 0; --n)
			{
				yield return new CPos(x, y);

				if (error > 0)
				{
					x += x_inc;
					error -= dy;
				}
				else
				{
					y += y_inc;
					error += dx;
				}
			}
		}
	}
}
