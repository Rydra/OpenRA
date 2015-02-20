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
using System.Drawing;
using System.Linq;
using OpenRA.Activities;
using OpenRA.Mods.Common.Activities;
using OpenRA.Primitives;
using OpenRA.Traits;

namespace OpenRA.Mods.Common.Traits
{
	#region Interfaces and Enums

	[Flags]
	public enum CellConditions
	{
		None = 0,
		TransientActors,
		BlockedByMovers,
		All = TransientActors | BlockedByMovers
	}

	public interface IMobileInfo : IMoveInfo
	{
		int ROT { get; }

		/// <summary>
		/// Indiates whether this unit can share his cell
		/// with other units (i.e. infantry)
		/// </summary>
		bool SharesCell { get; }
		int InitialFacing { get; }
		string[] Crushes { get; }
		bool MoveIntoShroud { get; }
		bool OnRails { get; }
		int WaitAverage { get; }
		int WaitSpread { get; }
		int Speed { get; }

		/// <summary>
		/// Calculates a hash for a given tileSet. Useful
		/// to determine unique movement conditions for units
		/// </summary>
		int TileSetMovementHash(ITileSet tileSet);

		/// <summary>
		/// Determines whether the actor is blocked by other Actors
		/// </summary>
		/// <param name="world">The world where the actor moves</param>
		/// <param name="self">The actor to check</param>
		/// <param name="cell">The cell to check</param>
		/// <param name="ignoreActor">The actor to ignore collision, if any</param>
		/// <param name="check">Any applicable constraints</param>
		bool CanMoveFreelyInto(IWorld world, IActor self, CPos cell, IActor ignoreActor, CellConditions check);

		/// <summary>
		/// Determines whether the actor is blocked by other Actors
		/// </summary>
		/// <param name="world">The world where the actor moves</param>
		/// <param name="self">The actor to check</param>
		/// <param name="cell">The cell to check</param>
		/// <param name="ignoreActor">The actor to ignore collision, if any</param>
		/// <param name="check">Any applicable constraints</param>
		bool CollidesWithOtherActorsInCell(IWorld world, IActor self, CPos cell, IActor ignoreActor, CellConditions check);

		int MovementCostForCell(IWorld world, CPos cell);
		bool CanEnterCell(IWorld world, IActor self, CPos cell, out int movementCost, IActor ignoreActor = null, CellConditions check = CellConditions.All);
		bool CanEnterCell(IWorld world, IActor self, CPos cell, IActor ignoreActor = null, CellConditions check = CellConditions.All);
		int GetMovementClass(ITileSet tileset);
		SubCell GetAvailableSubCell(
			IWorld world,
			IActor self,
			CPos cell,
			SubCell preferredSubCell = SubCell.Any,
			IActor ignoreActor = null,
			CellConditions check = CellConditions.All);

		SubCell CheckAvailableSubCell(
			IWorld world,
			IActor self,
			CPos cell,
			SubCell preferredSubCell,
			IActor ignoreActor,
			CellConditions check);

		Cache<ITileSet, TerrainInfo[]> TilesetTerrainInfo { get; }
	}

	#endregion

	public class TerrainInfo
	{
		public static readonly TerrainInfo Impassable = new TerrainInfo();

		public readonly int Cost;
		public readonly decimal Speed;

		public TerrainInfo()
		{
			Cost = int.MaxValue;
			Speed = 0;
		}

		public TerrainInfo(decimal speed, int cost)
		{
			Speed = speed;
			Cost = cost;
		}
	}

	[Desc("Unit is able to move.")]
	public class MobileInfo : IMobileInfo, IOccupySpaceInfo, IFacingInfo, UsesInit<FacingInit>, UsesInit<LocationInit>, UsesInit<SubCellInit>
	{
		#region Properties

		[FieldLoader.LoadUsing("LoadSpeeds")]
		[Desc("Set Water: 0 for ground units and lower the value on rough terrain.")]
		public readonly Dictionary<string, TerrainInfo> TerrainSpeeds;

		[Desc("e.g. crate, wall, infantry")]
		public readonly string[] Crushes = { };
		string[] IMobileInfo.Crushes
		{
			get { return Crushes; }
		}

		public readonly int WaitAverage = 5;

		int IMobileInfo.WaitAverage
		{
			get { return WaitAverage; }
		}

		public readonly int WaitSpread = 2;
		int IMobileInfo.WaitSpread
		{
			get { return WaitSpread; }
		}

		public readonly int InitialFacing = 0;
		int IMobileInfo.InitialFacing
		{
			get { return InitialFacing; }
		}

		[Desc("Rate of Turning")]
		public readonly int ROT = 255;
		int IMobileInfo.ROT
		{
			get { return ROT; }
		}

		public readonly int Speed = 1;
		int IMobileInfo.Speed
		{
			get { return Speed; }
		}

		public readonly bool OnRails = false;
		bool IMobileInfo.OnRails
		{
			get { return OnRails; }
		}

		[Desc("Allow multiple (infantry) units in one cell.")]
		public readonly bool SharesCell = false;
		bool IMobileInfo.SharesCell
		{
			get { return SharesCell; }
		}

		[Desc("Can the actor be ordered to move in to shroud?")]
		public readonly bool MoveIntoShroud = true;

		bool IMobileInfo.MoveIntoShroud
		{
			get { return MoveIntoShroud; }
		}

		public readonly string Cursor = "move";
		public readonly string BlockedCursor = "move-blocked";

		public readonly Cache<ITileSet, int> TilesetMovementClass;
		public readonly Cache<ITileSet, TerrainInfo[]> TilesetTerrainInfo;
		Cache<ITileSet, TerrainInfo[]> IMobileInfo.TilesetTerrainInfo
		{
			get { return TilesetTerrainInfo; }
		}

		#endregion

		public virtual object Create(ActorInitializer init) { return new Mobile(init, this); }

		static object LoadSpeeds(MiniYaml y)
		{
			var ret = new Dictionary<string, TerrainInfo>();
			foreach (var t in y.ToDictionary()["TerrainSpeeds"].Nodes)
			{
				var speed = FieldLoader.GetValue<decimal>("speed", t.Value.Value);
				var nodesDict = t.Value.ToDictionary();
				var cost = nodesDict.ContainsKey("PathingCost")
					? FieldLoader.GetValue<int>("cost", nodesDict["PathingCost"].Value)
					: (int)(10000 / speed);
				ret.Add(t.Key, new TerrainInfo(speed, cost));
			}

			return ret;
		}

		TerrainInfo[] LoadTilesetSpeeds(ITileSet tileSet)
		{
			var info = new TerrainInfo[tileSet.TerrainInfo.Length];
			for (var i = 0; i < info.Length; i++)
				info[i] = TerrainInfo.Impassable;

			foreach (var kvp in TerrainSpeeds)
			{
				byte index;
				if (tileSet.TryGetTerrainIndex(kvp.Key, out index))
					info[index] = kvp.Value;
			}

			return info;
		}

		public MobileInfo()
		{
			TilesetTerrainInfo = new Cache<ITileSet, TerrainInfo[]>(LoadTilesetSpeeds);
			TilesetMovementClass = new Cache<ITileSet, int>(CalculateTilesetMovementClass);
		}

		/// <summary>
		/// This constructor is merely used for testing since I can't
		/// assign nor mock readonly public fields.
		/// </summary>
		public MobileInfo(bool sharesCell, string[] crushes)
		{
			SharesCell = sharesCell;
			Crushes = crushes;
		}

		public int MovementCostForCell(IWorld world, CPos cell)
		{
			if (!world.Map.Contains(cell))
				return int.MaxValue;

			var index = world.Map.GetTerrainIndex(cell);
			return index == byte.MaxValue ? int.MaxValue : TilesetTerrainInfo[world.TileSet][index].Cost;
		}

		public int CalculateTilesetMovementClass(ITileSet tileset)
		{
			// collect our ability to cross *all* terraintypes, in a bitvector
			return TilesetTerrainInfo[tileset].Select(ti => ti.Cost < int.MaxValue).ToBits();
		}

		public int GetMovementClass(ITileSet tileset)
		{
			return TilesetMovementClass[tileset];
		}

		public int TileSetMovementHash(ITileSet tileSet)
		{
			var terrainInfos = TilesetTerrainInfo[tileSet];

			// Compute and return the hash using aggregate
			return terrainInfos.Aggregate(terrainInfos.Length,
				(current, terrainInfo) => unchecked(current * 31 + terrainInfo.Cost));
		}

		public bool CanEnterCell(IWorld world, IActor self, CPos cell, IActor ignoreActor = null, CellConditions check = CellConditions.All)
		{
			return MovementCostForCell(world, cell) != int.MaxValue &&
				CanMoveFreelyInto(world, self, cell, ignoreActor, check);
		}

		public bool CanMoveFreelyInto(IWorld world, IActor self, CPos cell, IActor ignoreActor = null, CellConditions check = CellConditions.All)
		{
			// If the actor can share his cell and the cell he intends to enter
 			// has any free space for him, then it can move into it.
			// (or whether it's told we don't need to check for transient actor blockages)
			if ((SharesCell && world.ActorMap.HasFreeSubCell(cell)) ||
				!check.HasFlag(CellConditions.TransientActors))
				return true;

			// If the actor cannot enter outrightly, we must check if it can
			// crush the units inside the cell if they are enemies. If they are
			// allies, we must check if they follow our direction and can ignore them
			var canIgnoreMovingAllies = !check.HasFlag(CellConditions.BlockedByMovers);

			foreach (var actor in world.ActorMap.GetActorsAt(cell))
			{
				if (Collides(self, actor, ignoreActor, canIgnoreMovingAllies))
					return false;
			}

			return true;
		}

		public bool CollidesWithOtherActorsInCell(IWorld world, IActor self, CPos cell, IActor ignoreActor = null, CellConditions check = CellConditions.All)
		{
			// If the actor can share his cell and the cell he intends to enter
			// has any free space for him, then it can move into it.
			// (or whether it's told we don't need to check for transient actor blockages)
			if ((SharesCell && world.ActorMap.HasFreeSubCell(cell)) ||
				!check.HasFlag(CellConditions.TransientActors))
				return false;

			// If the actor cannot enter outrightly, we must check if it can
			// crush the units inside the cell if they are enemies. If they are
			// allies, we must check if they follow our direction and can ignore them
			var canIgnoreMovingAllies = !check.HasFlag(CellConditions.BlockedByMovers);

			foreach (var actor in world.ActorMap.GetActorsAt(cell))
			{
				if (Collides(self, actor, ignoreActor, canIgnoreMovingAllies))
					return true;
			}

			return false;
		}

		public bool CanEnterCell(IWorld world, IActor self, CPos cell, out int movementCost, IActor ignoreActor = null, CellConditions check = CellConditions.All)
		{
			return (movementCost = MovementCostForCell(world, cell)) != int.MaxValue &&
				CanMoveFreelyInto(world, self, cell, ignoreActor, check);
		}

		public SubCell GetAvailableSubCell(IWorld world, IActor self, CPos cell, SubCell preferredSubCell = SubCell.Any, IActor ignoreActor = null, CellConditions check = CellConditions.All)
		{
			if (MovementCostForCell(world, cell) == int.MaxValue)
				return SubCell.Invalid;

			return CheckAvailableSubCell(world, self, cell, preferredSubCell, ignoreActor, check);
		}

		public SubCell CheckAvailableSubCell(IWorld world, IActor self, CPos cell, SubCell preferredSubCell,
			IActor ignoreActor, CellConditions check)
		{
			if (check.HasFlag(CellConditions.TransientActors))
			{
				var canIgnoreMovingAllies = self != null && !check.HasFlag(CellConditions.BlockedByMovers);
				Func<IActor, bool> checkTransient = a => Collides(self, a, ignoreActor, canIgnoreMovingAllies);

				if (!SharesCell)
					return world.ActorMap.AnyUnitsAt(cell, SubCell.FullCell, checkTransient) ? SubCell.Invalid : SubCell.FullCell;

				return world.ActorMap.FreeSubCell(cell, preferredSubCell, checkTransient);
			}

			if (!SharesCell)
				return world.ActorMap.AnyUnitsAt(cell, SubCell.FullCell) ? SubCell.Invalid : SubCell.FullCell;

			return world.ActorMap.FreeSubCell(cell, preferredSubCell);
		}

		public int GetInitialFacing() { return InitialFacing; }

		/// <summary>
		/// Checks whether the actor "self" collides against the actor "actor" or whether
		/// it has to ignore the "ignoreActor"
		/// </summary>
		bool Collides(IActor self, IActor actor, IActor ignoreActor, bool canIgnoreMovingAllies)
		{
			if (actor == ignoreActor)
				return false;

			// Neutral/enemy units are blockers. Allied units that are moving are not blockers.
			if (canIgnoreMovingAllies && self.IsAlliedWith(actor) && IsMovingInMyDirection(self, actor))
				return false;

			// Non-sharable unit can enter a cell with shareable units only if it can crush all of them.
			if (CanBeCrushedBy(self, actor))
				return false;

			return true;
		}

		bool CanBeCrushedBy(IActor crusher, IActor actor)
		{
			if (Crushes == null || Crushes.Length == 0)
				return false;

			var crushables = actor.TraitsImplementing<ICrushable>();
			if (!crushables.Any() || crushables.Any(crushable => !crushable.CrushableBy(Crushes, crusher.Owner)))
				return false;

			return true;
		}

		static bool IsMovingInMyDirection(IActor self, IActor other)
		{
			if (!other.IsMoving()) return false;

			if (self == null) return true;
			var selfMobile = self.TraitOrDefault<IMobile>();
			if (selfMobile == null) return false;

			var otherMobile = other.TraitOrDefault<IMobile>();
			if (otherMobile == null) return false;

			// Sign of dot-product indicates (roughly) if vectors are facing in same or opposite directions:
			var dp = CVec.Dot(selfMobile.ToCell - self.Location, otherMobile.ToCell - other.Location);
			if (dp <= 0) return false;

			return true;
		}
	}

	/// <summary>
	/// Defines the current status of the unit that is moving
	/// </summary>
	public interface IMobile
	{
		/// <summary>
		/// Defines the cell from where the unit is moving. FromCell and ToCell
		/// are continguous cells in the map
		/// </summary>
		[Sync] CPos FromCell { get; }

		/// <summary>
		/// Defines the cell that the unit wants to enter.
		/// </summary>
		[Sync] CPos ToCell { get; }

		IMobileInfo Info { get; }

		/// <summary>
		/// Defines the subCell the unit is placed now.
		/// </summary>
		SubCell FromSubCell { get; set; }

		/// <summary>
		/// Defines the subCell the unit intends to enter.
		/// </summary>
		SubCell ToSubCell { get; set; }

		[Sync] int PathHash { get; set; }
		bool IsMoving { get; set; }
		int TicksBeforePathing { get; set; }
		[Sync] int Facing { get; set; }

		bool CollidesWithOtherActorsInCell(CPos cell, IActor ignoreActor = null, bool checkTransientActors = true);
		void SetLocation(CPos from, SubCell fromSub, CPos to, SubCell toSub);

		SubCell GetAvailableSubCell(CPos a, SubCell preferredSubCell = SubCell.Any, IActor ignoreActor = null,
			bool checkTransientActors = true);

		SubCell CheckAvailableSubCell(CPos a, SubCell preferredSubCell = SubCell.Any, IActor ignoreActor = null,
			bool checkTransientActors = true);
		void RemoveInfluence();
		void AddInfluence();
		int MovementSpeedForCell(IActor self, CPos cell);
		void FinishedMoving(IActor self);
		void EnteringCell(IActor self);
		void SetPosition(IActor self, WPos pos);
		void SetPosition(IActor self, CPos cell, SubCell subCell = SubCell.Any);
		void SetVisualPosition(IActor self, WPos pos);

		Activity MoveTo(Func<List<CPos>> pathFunc);
		bool CanEnterCell(CPos cell, IActor ignoreActor = null, bool checkTransientActors = true);
	}

	public class Mobile : IIssueOrder, IResolveOrder, IOrderVoice, IPositionable, IMove, IFacing, ISync, INotifyAddedToWorld, INotifyRemovedFromWorld, INotifyBlockingMove, IMobile
	{
		const int AverageTicksBeforePathing = 5;
		const int SpreadTicksBeforePathing = 5;

		internal int TicksBeforePathing = 0;
		int IMobile.TicksBeforePathing
		{
			get { return TicksBeforePathing; }
			set { TicksBeforePathing = value; }
		}

		readonly IActor self;

		public readonly IMobileInfo Info;
		IMobileInfo IMobile.Info { get { return this.Info; } }

		public bool IsMoving { get; set; }

		int facing;
		CPos fromCell, toCell;
		public SubCell FromSubCell, ToSubCell;

		[Sync]
		public int Facing
		{
			get { return facing; }
			set { facing = value; }
		}

		public int ROT { get { return Info.ROT; } }

		[Sync] public WPos CenterPosition { get; private set; }
		[Sync] public CPos FromCell { get { return fromCell; } }
		[Sync] public CPos ToCell { get { return toCell; } }

		SubCell IMobile.ToSubCell
		{
			get { return ToSubCell; }
			set { ToSubCell = value; }
		}

		SubCell IMobile.FromSubCell
		{
			get { return FromSubCell; }
			set { FromSubCell = value; }
		}

		[Sync] public int PathHash;	// written by Move.EvalPath, to temporarily debug this crap.

		int IMobile.PathHash
		{
			get { return PathHash; }
			set { PathHash = value; }
		}

		public void SetLocation(CPos from, SubCell fromSub, CPos to, SubCell toSub)
		{
			if (FromCell == from && ToCell == to && FromSubCell == fromSub && ToSubCell == toSub)
				return;

			RemoveInfluence();
			fromCell = from;
			toCell = to;
			FromSubCell = fromSub;
			ToSubCell = toSub;
			AddInfluence();
		}

		public Mobile(IActorInitializer init, IMobileInfo info)
		{
			self = init.Self;
			Info = info;

			ToSubCell = FromSubCell = info.SharesCell ? init.World.Map.DefaultSubCell : SubCell.FullCell;
			if (init.Contains<SubCellInit>())
				FromSubCell = ToSubCell = init.Get<SubCellInit, SubCell>();

			if (init.Contains<LocationInit>())
			{
				fromCell = toCell = init.Get<LocationInit, CPos>();
				SetVisualPosition(init.Self, init.World.Map.CenterOfSubCell(FromCell, FromSubCell));
			}

			Facing = init.Contains<FacingInit>() ? init.Get<FacingInit, int>() : info.InitialFacing;

			// Sets the visual position to WPos accuracy
			// Use LocationInit if you want to insert the actor into the ActorMap!
			if (init.Contains<CenterPositionInit>())
				SetVisualPosition(init.Self, init.Get<CenterPositionInit, WPos>());
		}

		// Returns a valid sub-cell
		public SubCell GetValidSubCell(SubCell preferred = SubCell.Any)
		{
			// Try same sub-cell
			if (preferred == SubCell.Any)
				preferred = FromSubCell;

			// Fix sub-cell assignment
			if (Info.SharesCell)
			{
				if (preferred <= SubCell.FullCell)
					return self.World.Map.DefaultSubCell;
			}
			else
			{
				if (preferred != SubCell.FullCell)
					return SubCell.FullCell;
			}

			return preferred;
		}

		public void SetPosition(IActor self, CPos cell, SubCell subCell = SubCell.Any)
		{
			subCell = GetValidSubCell(subCell);
			SetLocation(cell, subCell, cell, subCell);
			SetVisualPosition(self, self.World.Map.CenterOfSubCell(cell, subCell));
			FinishedMoving(self);
		}

		public void SetPosition(IActor self, WPos pos)
		{
			var cell = self.World.Map.CellContaining(pos);
			SetLocation(cell, FromSubCell, cell, FromSubCell);
			SetVisualPosition(self, pos);
			FinishedMoving(self);
		}

		public void SetVisualPosition(IActor self, WPos pos)
		{
			CenterPosition = pos;
			if (self.IsInWorld)
			{
				self.World.ScreenMap.Update(self as Actor);
				self.World.ActorMap.UpdatePosition(self, this);
			}
		}

		public void AddedToWorld(IActor self)
		{
			var actor = self as Actor;
			self.World.ActorMap.AddInfluence(actor, this);
			self.World.ActorMap.AddPosition(actor, this);
			self.World.ScreenMap.Add(actor);
		}

		public void RemovedFromWorld(IActor self)
		{
			self.World.ActorMap.RemoveInfluence(self, this);
			self.World.ActorMap.RemovePosition(self, this);
			self.World.ScreenMap.Remove(self as Actor);
		}

		public IEnumerable<IOrderTargeter> Orders { get { yield return new MoveOrderTargeter(self, Info as MobileInfo); } }

		// Note: Returns a valid order even if the unit can't move to the target
		public Order IssueOrder(IActor self, IOrderTargeter order, Target target, bool queued)
		{
			if (order is MoveOrderTargeter)
			{
				if (Info.OnRails)
					return null;

				return new Order("Move", self as Actor, queued) { TargetLocation = self.World.Map.CellContaining(target.CenterPosition) };
			}

			return null;
		}

		public CPos NearestMoveableCell(CPos target)
		{
			// Limit search to a radius of 10 tiles
			return NearestMoveableCell(target, 1, 10);
		}

		public CPos NearestMoveableCell(CPos target, int minRange, int maxRange)
		{
			if (CanEnterCell(target))
				return target;

			foreach (var tile in self.World.Map.FindTilesInAnnulus(target, minRange, maxRange))
				if (CanEnterCell(tile))
					return tile;

			// Couldn't find a cell
			return target;
		}

		public CPos NearestCell(CPos target, Func<CPos, bool> check, int minRange, int maxRange)
		{
			if (check(target))
				return target;

			foreach (var tile in self.World.Map.FindTilesInAnnulus(target, minRange, maxRange))
				if (check(tile))
					return tile;

			// Couldn't find a cell
			return target;
		}

		void PerformMoveInner(IActor self, CPos targetLocation, bool queued)
		{
			var currentLocation = NearestMoveableCell(targetLocation);

			if (!CanEnterCell(currentLocation))
			{
				if (queued) self.CancelActivity();
				return;
			}

			if (!queued) self.CancelActivity();

			TicksBeforePathing = AverageTicksBeforePathing + self.World.SharedRandom.Next(-SpreadTicksBeforePathing, SpreadTicksBeforePathing);

			self.QueueActivity(new Move(self, currentLocation, 8));

			self.SetTargetLine(Target.FromCell(self.World, currentLocation), Color.Green);
		}

		protected void PerformMove(IActor self, CPos targetLocation, bool queued)
		{
			if (queued)
				self.QueueActivity(new CallFunc(() => PerformMoveInner(self, targetLocation, true)));
			else
				PerformMoveInner(self, targetLocation, false);
		}

		public void ResolveOrder(IActor self, Order order)
		{
			if (order.OrderString == "Move")
			{
				if (!Info.MoveIntoShroud && !self.Owner.Shroud.IsExplored(order.TargetLocation))
					return;

				PerformMove(self, self.World.Map.Clamp(order.TargetLocation),
					order.Queued && !self.IsIdle);
			}

			if (order.OrderString == "Stop")
				self.CancelActivity();

			if (order.OrderString == "Scatter")
				Nudge(self, self, true);
		}

		public string VoicePhraseForOrder(IActor self, Order order)
		{
			switch (order.OrderString)
			{
				case "Move":
				case "Scatter":
				case "Stop":
					return "Move";
				default:
					return null;
			}
		}

		public CPos TopLeft { get { return ToCell; } }

		public IEnumerable<Pair<CPos, SubCell>> OccupiedCells()
		{
			if (FromCell == ToCell)
				return new[] { Pair.New(FromCell, FromSubCell) };
			if (CanEnterCell(ToCell))
				return new[] { Pair.New(ToCell, ToSubCell) };
			return new[] { Pair.New(FromCell, FromSubCell), Pair.New(ToCell, ToSubCell) };
		}

		public bool IsLeavingCell(CPos location, SubCell subCell = SubCell.Any)
		{
			return ToCell != location && fromCell == location
				&& (subCell == SubCell.Any || FromSubCell == subCell || subCell == SubCell.FullCell || FromSubCell == SubCell.FullCell);
		}

		public SubCell GetAvailableSubCell(CPos a, SubCell preferredSubCell = SubCell.Any, IActor ignoreActor = null, bool checkTransientActors = true)
		{
			return Info.GetAvailableSubCell(self.World, self, a, preferredSubCell, ignoreActor, checkTransientActors ? CellConditions.All : CellConditions.None);
		}

		public SubCell CheckAvailableSubCell(CPos a, SubCell preferredSubCell = SubCell.Any, IActor ignoreActor = null, bool checkTransientActors = true)
		{
			return Info.CheckAvailableSubCell(self.World, self, a, preferredSubCell, ignoreActor, checkTransientActors ? CellConditions.All : CellConditions.None);
		}

		public bool CanEnterCell(CPos cell, IActor ignoreActor = null, bool checkTransientActors = true)
		{
			return Info.CanEnterCell(self.World, self, cell, ignoreActor, checkTransientActors ? CellConditions.All : CellConditions.BlockedByMovers);
		}

		public bool CollidesWithOtherActorsInCell(CPos cell, IActor ignoreActor = null, bool checkTransientActors = true)
		{
			return Info.CollidesWithOtherActorsInCell(self.World, self, cell, ignoreActor, checkTransientActors ? CellConditions.All : CellConditions.BlockedByMovers);
		}

		public void EnteringCell(IActor self)
		{
			var crushables = self.World.ActorMap.GetUnitsAt(ToCell).Where(a => a != self)
				.SelectMany(a => a.TraitsImplementing<ICrushable>().Where(b => b.CrushableBy(Info.Crushes, self.Owner)));
			foreach (var crushable in crushables)
				crushable.WarnCrush(self as Actor);
		}

		public void FinishedMoving(IActor self)
		{
			var crushables = self.World.ActorMap.GetUnitsAt(ToCell).Where(a => a != self)
				.SelectMany(a => a.TraitsImplementing<ICrushable>().Where(c => c.CrushableBy(Info.Crushes, self.Owner)));
			foreach (var crushable in crushables)
				crushable.OnCrush(self as Actor);
		}

		public int MovementSpeedForCell(IActor self, CPos cell)
		{
			var index = self.World.Map.GetTerrainIndex(cell);
			if (index == byte.MaxValue)
				return 0;

			// TODO: Convert to integers
			var speed = Info.TilesetTerrainInfo[self.World.TileSet][index].Speed;
			if (speed == decimal.Zero)
				return 0;

			speed *= Info.Speed;
			foreach (var t in self.TraitsImplementing<ISpeedModifier>())
				speed *= t.GetSpeedModifier() / 100m;

			return (int)(speed / 100);
		}

		public void AddInfluence()
		{
			if (self.IsInWorld)
				self.World.ActorMap.AddInfluence(self, this);
		}

		public void RemoveInfluence()
		{
			if (self.IsInWorld)
				self.World.ActorMap.RemoveInfluence(self, this);
		}

		public void Nudge(IActor self, IActor nudger, bool force)
		{
			/* initial fairly braindead implementation. */
			if (!force && self.Owner.Stances[nudger.Owner] != Stance.Ally)
				return;		/* don't allow ourselves to be pushed around
							 * by the enemy! */

			if (!force && !self.IsIdle)
				return;		/* don't nudge if we're busy doing something! */

			// pick an adjacent available cell.
			var availCells = new List<CPos>();
			var notStupidCells = new List<CPos>();

			for (var i = -1; i < 2; i++)
				for (var j = -1; j < 2; j++)
				{
					var p = ToCell + new CVec(i, j);
					if (CanEnterCell(p))
						availCells.Add(p);
					else
						if (p != nudger.Location && p != ToCell)
							notStupidCells.Add(p);
				}

			var moveTo = availCells.Any() ? availCells.Random(self.World.SharedRandom) : (CPos?)null;

			if (moveTo.HasValue)
			{
				// Isn't it supposed that the actor didn't have any activity?
				self.CancelActivity();
				self.SetTargetLine(Target.FromCell(self.World, moveTo.Value), Color.Green, false);
				self.QueueActivity(new Move(self, moveTo.Value, 0));

				Log.Write("debug", "OnNudge #{0} from {1} to {2}",
					self.ActorID, self.Location, moveTo.Value);
			}
			else
			{
				var cellInfo = notStupidCells
					.SelectMany(c => self.World.ActorMap.GetActorsAt(c)
						.Where(a => a.IsIdle && a.HasTrait<IMobile>()),
						(c, a) => new { Cell = c, Actor = a })
					.RandomOrDefault(self.World.SharedRandom);

				if (cellInfo != null)
				{
					self.CancelActivity();
					var notifyBlocking = new CallFunc(() => self.NotifyBlocker(cellInfo.Cell));
					var waitFor = new WaitFor(() => CanEnterCell(cellInfo.Cell));
					var move = new Move(self, cellInfo.Cell);
					self.QueueActivity(Util.SequenceActivities(notifyBlocking, waitFor, move));

					Log.Write("debug", "OnNudge (notify next blocking actor, wait and move) #{0} from {1} to {2}",
						self.ActorID, self.Location, cellInfo.Cell);
				}
				else
				{
					Log.Write("debug", "OnNudge #{0} refuses at {1}",
						self.ActorID, self.Location);
				}
			}
		}

		class MoveOrderTargeter : IOrderTargeter
		{
			readonly MobileInfo unitType;
			readonly bool rejectMove;

			public MoveOrderTargeter(IActor self, MobileInfo unitType)
			{
				this.unitType = unitType;
				this.rejectMove = !self.AcceptsOrder("Move");
			}

			public string OrderID { get { return "Move"; } }
			public int OrderPriority { get { return 4; } }
			public bool IsQueued { get; protected set; }

			public bool CanTarget(Actor self, Target target, List<Actor> othersAtTarget, TargetModifiers modifiers, ref string cursor)
			{
				if (rejectMove || !target.IsValidFor(self))
					return false;

				var location = self.World.Map.CellContaining(target.CenterPosition);
				IsQueued = modifiers.HasModifier(TargetModifiers.ForceQueue);

				var explored = self.Owner.Shroud.IsExplored(location);
				cursor = self.World.Map.Contains(location) ?
					(self.World.Map.GetTerrainInfo(location).CustomCursor ?? unitType.Cursor) : unitType.BlockedCursor;

				if ((!explored && !unitType.MoveIntoShroud) || (explored && unitType.MovementCostForCell(self.World, location) == int.MaxValue))
					cursor = unitType.BlockedCursor;

				return true;
			}
		}

		public Activity ScriptedMove(CPos cell) { return new Move(self, cell); }
		public Activity MoveTo(CPos cell, int nearEnough) { return new Move(self, cell, nearEnough); }
		public Activity MoveTo(CPos cell, IActor ignoredActor) { return new Move(self, cell, ignoredActor); }
		public Activity MoveWithinRange(Target target, WRange range) { return new MoveWithinRange(self, target, WRange.Zero, range); }
		public Activity MoveWithinRange(Target target, WRange minRange, WRange maxRange) { return new MoveWithinRange(self, target, minRange, maxRange); }
		public Activity MoveFollow(IActor self, Target target, WRange minRange, WRange maxRange) { return new Follow(self as Actor, target, minRange, maxRange); }
		public Activity MoveTo(Func<List<CPos>> pathFunc) { return new Move(self, pathFunc); }

		public void OnNotifyBlockingMove(IActor self, IActor blocking)
		{
			if (self.IsIdle && self.AppearsFriendlyTo(blocking))
				Nudge(self, blocking, true);
		}

		public Activity MoveIntoWorld(IActor self, CPos cell, SubCell subCell = SubCell.Any)
		{
			var pos = self.CenterPosition;

			if (subCell == SubCell.Any)
				subCell = self.World.ActorMap.FreeSubCell(cell, subCell);

			// TODO: solve/reduce cell is full problem
			if (subCell == SubCell.Invalid)
				subCell = self.World.Map.DefaultSubCell;

			// Reserve the exit cell
			SetPosition(self, cell, subCell);
			SetVisualPosition(self, pos);

			return VisualMove(self, pos, self.World.Map.CenterOfSubCell(cell, subCell), cell);
		}

		public Activity MoveToTarget(IActor self, Target target)
		{
			if (target.Type == TargetType.Invalid)
				return null;

			return new MoveAdjacentTo(self as Actor, target);
		}

		public Activity MoveIntoTarget(IActor self, Target target)
		{
			if (target.Type == TargetType.Invalid)
				return null;

			return VisualMove(self, self.CenterPosition, target.CenterPosition);
		}

		public bool CanEnterTargetNow(IActor self, Target target)
		{
			return self.Location == self.World.Map.CellContaining(target.CenterPosition) || Util.AdjacentCells(self.World as World, target).Any(c => c == self.Location);
		}

		public Activity VisualMove(IActor self, WPos fromPos, WPos toPos)
		{
			return VisualMove(self, fromPos, toPos, self.Location);
		}

		public Activity VisualMove(IActor self, WPos fromPos, WPos toPos, CPos cell)
		{
			var speed = MovementSpeedForCell(self, cell);
			var length = speed > 0 ? (toPos - fromPos).Length / speed : 0;

			var facing = Util.GetFacing(toPos - fromPos, Facing);
			var actor = self as Actor;
			return Util.SequenceActivities(new Turn(actor, facing), new Drag(actor, fromPos, toPos, length));
		}

		// Eventually these explicit implementations should be removed
		#region Explicit implementations

		Order IIssueOrder.IssueOrder(Actor self, IOrderTargeter order, Target target, bool queued)
		{
			return IssueOrder(self, order, target, queued);
		}

		void IResolveOrder.ResolveOrder(Actor self, Order order)
		{
			ResolveOrder(self, order);
		}

		string IOrderVoice.VoicePhraseForOrder(Actor self, Order order)
		{
			return VoicePhraseForOrder(self, order);
		}

		bool IPositionable.CanEnterCell(CPos location, Actor ignoreActor, bool checkTransientActors)
		{
			return CanEnterCell(location, ignoreActor, checkTransientActors);
		}

		SubCell IPositionable.GetAvailableSubCell(CPos location, SubCell preferredSubCell, Actor ignoreActor, bool checkTransientActors)
		{
			return GetAvailableSubCell(location, preferredSubCell, ignoreActor, checkTransientActors);
		}

		void IPositionable.SetPosition(Actor self, CPos cell, SubCell subCell)
		{
			SetPosition(self, cell, subCell);
		}

		void IPositionable.SetPosition(Actor self, WPos pos)
		{
			SetPosition(self, pos);
		}

		void IPositionable.SetVisualPosition(Actor self, WPos pos)
		{
			SetVisualPosition(self, pos);
		}

		Activity IMove.MoveTo(CPos cell, Actor ignoredActor)
		{
			return MoveTo(cell, ignoredActor);
		}

		Activity IMove.MoveFollow(Actor self, Target target, WRange minRange, WRange maxRange)
		{
			return MoveFollow(self, target, minRange, maxRange);
		}

		Activity IMove.MoveIntoWorld(Actor self, CPos cell, SubCell subCell)
		{
			return MoveIntoWorld(self, cell, subCell);
		}

		Activity IMove.MoveToTarget(Actor self, Target target)
		{
			return MoveToTarget(self, target);
		}

		Activity IMove.MoveIntoTarget(Actor self, Target target)
		{
			return MoveIntoTarget(self, target);
		}

		Activity IMove.VisualMove(Actor self, WPos fromPos, WPos toPos)
		{
			return VisualMove(self, fromPos, toPos);
		}

		bool IMove.CanEnterTargetNow(Actor self, Target target)
		{
			return CanEnterTargetNow(self, target);
		}

		void INotifyAddedToWorld.AddedToWorld(Actor self)
		{
			AddedToWorld(self);
		}

		void INotifyRemovedFromWorld.RemovedFromWorld(Actor self)
		{
			RemovedFromWorld(self);
		}

		void INotifyBlockingMove.OnNotifyBlockingMove(Actor self, Actor blocking)
		{
			OnNotifyBlockingMove(self, blocking);
		}

		#endregion
	}
}
