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
using OpenRA.Mods.Common.Traits;
using OpenRA.Traits;

// Classes from this file should be used in
// testing when we want to microbenchmark
namespace OpenRA.Test
{
	public class FakeActor : IActor
	{
		IWorld world;

		public ActorInfo Info
		{
			get { throw new NotImplementedException("No need to implement this yet"); }
		}

		public IWorld World
		{
			get { return world; }
		}

		public uint ActorID
		{
			get { return 1; }
		}

		public Player Owner
		{
			get { return null; }
			set { }
		}

		public T TraitOrDefault<T>()
		{
			return default(T);
		}

		public T Trait<T>()
		{
			return default(T);
		}

		public IEnumerable<T> TraitsImplementing<T>()
		{
			throw new NotImplementedException("No need to implement this yet");
		}

		public T TraitInfo<T>()
		{
			return default(T);
		}

		public IEnumerable<Graphics.IRenderable> Render(Graphics.WorldRenderer wr)
		{
			throw new NotImplementedException("No need to implement this yet");
		}

		public FakeActor(IWorld world)
		{
			// TODO: Complete member initialization
			this.world = world;
		}

		public FakeActor()
		{
			// TODO: Complete member initialization
		}

		public CPos Location
		{
			get { throw new NotImplementedException("No need to implement this yet"); }
		}

		public bool IsInWorld
		{
			get { throw new NotImplementedException("No need to implement this yet"); }
		}

		public bool Destroyed
		{
			get { throw new NotImplementedException("No need to implement this yet"); }
		}

		public bool IsIdle
		{
			get { throw new NotImplementedException("No need to implement this yet"); }
		}

		public Traits.IEffectiveOwner EffectiveOwner
		{
			get { throw new NotImplementedException("No need to implement this yet"); }
		}

		public WPos CenterPosition
		{
			get { throw new NotImplementedException("No need to implement this yet"); }
		}

		public Activities.Activity GetCurrentActivity()
		{
			throw new NotImplementedException("No need to implement this yet");
		}

		public bool HasTrait<T>()
		{
			throw new NotImplementedException("No need to implement this yet");
		}

		public void QueueActivity(bool queued, Activities.Activity nextActivity)
		{
			throw new NotImplementedException("No need to implement this yet");
		}

		public void QueueActivity(Activities.Activity nextActivity)
		{
			throw new NotImplementedException("No need to implement this yet");
		}

		public void CancelActivity()
		{
			throw new NotImplementedException("No need to implement this yet");
		}

		public IOccupySpace OccupiesSpace
		{
			get { throw new NotImplementedException(); }
		}

		public bool IsAlliedWith(IActor actor)
		{
			throw new NotImplementedException();
		}

		public bool IsDead
		{
			get { throw new NotImplementedException(); }
		}

		public int Generation
		{
			get { throw new NotImplementedException(); }
			set { throw new NotImplementedException(); }
		}
	}

	public class FakeWorld : IWorld
	{
		FakeActor worldactor;
		IMap map;

		public IActor WorldActor
		{
			get { return worldactor; }
		}

		public int WorldTick
		{
			get { return 50; }
		}

		public IMap Map
		{
			get { return map; }
		}

		public ITileSet TileSet
		{
			get { throw new NotImplementedException("No need to implement this yet"); }
		}

		public FakeWorld(IMap map)
		{
			// TODO: Complete member initialization
			this.map = map;
		}

		public FakeWorld(IMap map, FakeActor worldactor)
		{
			// TODO: Complete member initialization
			this.map = map;
			this.worldactor = worldactor;
		}

		public IActorMap ActorMap
		{
			get { throw new NotImplementedException("No need to implement this yet"); }
		}

		public Traits.ScreenMap ScreenMap
		{
			get { throw new NotImplementedException("No need to implement this yet"); }
		}

		public bool AllowDevCommands
		{
			get { throw new NotImplementedException("No need to implement this yet"); }
		}

		public IEnumerable<TraitPair<T>> ActorsWithTrait<T>()
		{
			throw new NotImplementedException("No need to implement this yet");
		}

		public Player LocalPlayer
		{
			get { throw new NotImplementedException("No need to implement this yet"); }
		}

		public Player RenderPlayer
		{
			get
			{
				throw new NotImplementedException("No need to implement this yet");
			}

			set
			{
				throw new NotImplementedException("No need to implement this yet");
			}
		}

		public Support.MersenneTwister SharedRandom
		{
			get { throw new NotImplementedException("No need to implement this yet"); }
		}

		public void AddFrameEndTask(Action<World> a)
		{
			throw new NotImplementedException("No need to implement this yet");
		}

		public IOrderGenerator OrderGenerator
		{
			get { throw new NotImplementedException(); }
			set { throw new NotImplementedException(); }
		}
	}

	public class FakeMobileInfo : IMobileInfo
	{
		Func<CPos, bool> conditions;

		public int MovementCostForCell(World world, CPos cell)
		{
			return 125;
		}

		public bool CanEnterCell(World world, Actor self, CPos cell, out int movementCost, Actor ignoreActor = null, CellConditions check = CellConditions.All)
		{
			movementCost = MovementCostForCell(world, cell);
			return conditions(cell);
		}

		public int GetMovementClass(TileSet tileset)
		{
			throw new NotImplementedException("No need to implement this yet");
		}

		public FakeMobileInfo(Func<CPos, bool> conditions)
		{
			this.conditions = conditions;
		}

		public bool CanEnterCell(World world, Actor self, CPos cell, Actor ignoreActor = null, CellConditions check = CellConditions.All)
		{
			return conditions(cell);
		}

		public int MovementCostForCell(IWorld world, CPos cell)
		{
			if (conditions(cell))
				return 125;
			return int.MaxValue;
		}

		public bool CanEnterCell(IWorld world, IActor self, CPos cell, out int movementCost, IActor ignoreActor = null, CellConditions check = CellConditions.All)
		{
			movementCost = MovementCostForCell(world, cell);
			return conditions(cell);
		}

		public bool CanEnterCell(IWorld world, IActor self, CPos cell, IActor ignoreActor = null, CellConditions check = CellConditions.All)
		{
			throw new NotImplementedException("No need to implement this yet");
		}

		public int GetMovementClass(ITileSet tileset)
		{
			throw new NotImplementedException("No need to implement this yet");
		}

		public int ROT
		{
			get { throw new NotImplementedException("No need to implement this yet"); }
		}

		public bool SharesCell
		{
			get { throw new NotImplementedException("No need to implement this yet"); }
		}

		public int InitialFacing
		{
			get { throw new NotImplementedException("No need to implement this yet"); }
		}

		public string[] Crushes
		{
			get { throw new NotImplementedException("No need to implement this yet"); }
		}

		public bool MoveIntoShroud
		{
			get { throw new NotImplementedException("No need to implement this yet"); }
		}

		public bool OnRails
		{
			get { throw new NotImplementedException("No need to implement this yet"); }
		}

		public int Speed
		{
			get { throw new NotImplementedException("No need to implement this yet"); }
		}

		public int TileSetMovementHash(ITileSet tileSet)
		{
			return 0;
		}

		public bool CanMoveFreelyInto(IWorld world, IActor self, CPos cell, IActor ignoreActor, CellConditions check)
		{
			throw new NotImplementedException("No need to implement this yet");
		}

		public Traits.SubCell GetAvailableSubCell(IWorld world, IActor self, CPos cell, Traits.SubCell preferredSubCell = SubCell.Any, IActor ignoreActor = null, CellConditions check = CellConditions.All)
		{
			throw new NotImplementedException("No need to implement this yet");
		}

		public Primitives.Cache<ITileSet, TerrainInfo[]> TilesetTerrainInfo
		{
			get { throw new NotImplementedException("No need to implement this yet"); }
		}

		public int WaitAverage
		{
			get { throw new NotImplementedException(); }
		}

		public int WaitSpread
		{
			get { throw new NotImplementedException(); }
		}

		public bool CollidesWithOtherActorsInCell(IWorld world, IActor self, CPos cell, IActor ignoreActor, CellConditions check)
		{
			throw new NotImplementedException();
		}

		public SubCell CheckAvailableSubCell(IWorld world, IActor self, CPos cell, SubCell preferredSubCell, IActor ignoreActor, CellConditions check)
		{
			throw new NotImplementedException();
		}

		public object Create(ActorInitializer init)
		{
			throw new NotImplementedException();
		}
	}

	public class FakeMap : IMap
	{
		int width;
		int height;

		public FakeMap(int width, int height)
		{
			// TODO: Complete member initialization
			this.width = width;
			this.height = height;
		}

		public TileShape TileShape
		{
			get { return TileShape.Rectangle; }
		}

		public int2 MapSize
		{
			get { return new int2(width, height); }
			set { throw new NotImplementedException("No need to implement this yet"); }
		}

		public bool Contains(CPos cell)
		{
			return cell.X >= 0 && cell.X < width && cell.Y >= 0 && cell.Y < height;
		}

		public CPos CellContaining(WPos pos)
		{
			throw new NotImplementedException("No need to implement this yet");
		}

		public WVec OffsetOfSubCell(Traits.SubCell subCell)
		{
			throw new NotImplementedException("No need to implement this yet");
		}

		public IEnumerable<CPos> FindTilesInCircle(CPos center, int maxRange)
		{
			throw new NotImplementedException("No need to implement this yet");
		}

		public WPos CenterOfCell(CPos cell)
		{
			throw new NotImplementedException("No need to implement this yet");
		}

		public byte GetTerrainIndex(CPos cell)
		{
			throw new NotImplementedException("No need to implement this yet");
		}

		public Traits.SubCell DefaultSubCell
		{
			get { throw new NotImplementedException("No need to implement this yet"); }
		}

		public IEnumerable<CPos> FindTilesInAnnulus(CPos center, int minRange, int maxRange)
		{
			throw new NotImplementedException("No need to implement this yet");
		}

		public CPos Clamp(CPos cell)
		{
			throw new NotImplementedException("No need to implement this yet");
		}

		public WPos CenterOfSubCell(CPos cell, Traits.SubCell subCell)
		{
			throw new NotImplementedException("No need to implement this yet");
		}

		public Ruleset Rules
		{
			get { throw new NotImplementedException(); }
		}

		public TerrainTypeInfo GetTerrainInfo(CPos cell)
		{
			throw new NotImplementedException();
		}

		public CellLayer<byte> CustomTerrain
		{
			get { throw new NotImplementedException(); }
		}

		public int FacingBetween(CPos cell, CPos towards, int fallbackfacing)
		{
			throw new NotImplementedException();
		}

		public WVec[] SubCellOffsets
		{
			get { throw new NotImplementedException(); }
		}
	}
}
