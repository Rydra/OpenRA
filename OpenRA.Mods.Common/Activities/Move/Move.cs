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
using System.Linq;
using OpenRA.Activities;
using OpenRA.Mods.Common.Pathfinder;
using OpenRA.Mods.Common.Traits;
using OpenRA.Primitives;
using OpenRA.Traits;

namespace OpenRA.Mods.Common.Activities
{
	using System.IO;

	public interface IPathCalculator
	{
		IEnumerable<CPos> CalculatePath();
	}

	public abstract class BasePathCalculator : IPathCalculator
	{
		protected readonly IPathFinder pathFinder;

		protected BasePathCalculator(IPathFinder pathFinder)
		{
			this.pathFinder = pathFinder;
		}

		public abstract IEnumerable<CPos> CalculatePath();
	}

	public class RegularPathFinder : BasePathCalculator
	{
		private readonly IActor self;
		private readonly IMobile mobile;
		private readonly CPos destination;

		public RegularPathFinder(IActor self, IMobile mobile, CPos destination)
			: base(self.World.WorldActor.Trait<IPathFinder>())
		{
			this.self = self;
			this.mobile = mobile;
			this.destination = destination;
		}

		public override IEnumerable<CPos> CalculatePath()
		{
			return
				pathFinder.FindPath(
					PathSearch.FromPoint(self.World, mobile.Info, self, mobile.ToCell, destination, false).WithoutLaneBias());
		} 
	}

	public class RegularPathfinderWithIgnoredActor : BasePathCalculator
	{
		private readonly IActor self;
		private readonly IMobile mobile;
		private readonly CPos destination;

		private readonly IActor ignoredActor;

		public RegularPathfinderWithIgnoredActor(IActor self, IMobile mobile, CPos destination, IActor ignoredActor)
			: base(self.World.WorldActor.Trait<IPathFinder>())
		{
			this.self = self;
			this.mobile = mobile;
			this.destination = destination;
			this.ignoredActor = ignoredActor;
		}

		public override IEnumerable<CPos> CalculatePath()
		{
			return
				pathFinder.FindPath(
					PathSearch.FromPoint(self.World, mobile.Info, self, mobile.ToCell, destination, false)
						.WithIgnoredActor(ignoredActor));
		}
	}

	public class FindUnitPathfinder : BasePathCalculator
	{
		private readonly IActor self;
		private readonly IMobile mobile;
		private readonly CPos destination;

		public FindUnitPathfinder(IActor self, IMobile mobile, CPos destination)
			: base(self.World.WorldActor.Trait<IPathFinder>())
		{
			this.self = self;
			this.mobile = mobile;
			this.destination = destination;
		}

		public override IEnumerable<CPos> CalculatePath()
		{
			return pathFinder.FindUnitPath(mobile.ToCell, destination, self);
		}
	}

	public class FindUnitRangefinder : BasePathCalculator
	{
		private readonly IActor self;
		private readonly IMobile mobile;
		private readonly CPos destination;

		private readonly SubCell subCell;

		private readonly WRange nearEnough;

		public FindUnitRangefinder(IActor self, IMobile mobile, CPos destination, SubCell subCell, WRange nearEnough)
			: base(self.World.WorldActor.Trait<IPathFinder>())
		{
			this.self = self;
			this.mobile = mobile;
			this.destination = destination;
			this.subCell = subCell;
			this.nearEnough = nearEnough;
		}

		public override IEnumerable<CPos> CalculatePath()
		{
			return pathFinder.FindUnitPathToRange(mobile.FromCell, subCell, self.World.Map.CenterOfSubCell(destination, subCell), nearEnough, self);
		}
	}

	public class SpecialCalculatePathToTarget : BasePathCalculator
	{
		private readonly IActor self;

		private readonly ITarget target;

		private readonly IMobile mobile;

		private readonly CPos targetPosition;

		private readonly DomainIndex domainIndex;

		private uint movementClass;

		public SpecialCalculatePathToTarget(IActor self, ITarget target, IMobile mobile, CPos targetPosition, DomainIndex domainIndex)
			: base(self.World.WorldActor.Trait<IPathFinder>())
		{
			this.self = self;
			this.target = target;
			this.mobile = mobile;
			this.targetPosition = targetPosition;
			this.domainIndex = domainIndex;
			movementClass = (uint)mobile.Info.GetMovementClass(self.World.TileSet);
		}

		protected virtual IEnumerable<CPos> CandidateMovementCells(IActor self)
		{
			return Util.AdjacentCells(self.World, target);
		}

		public override IEnumerable<CPos> CalculatePath()
		{
			var targetCells = CandidateMovementCells(self);
			var searchCells = new List<CPos>();
			var loc = self.Location;

			foreach (var cell in targetCells)
				if (domainIndex.IsPassable(loc, cell, movementClass) && mobile.CanEnterCell(cell))
					searchCells.Add(cell);

			if (!searchCells.Any())
				return new List<CPos>();

			var fromSrc = PathSearch.FromPoints(self.World, mobile.Info, self, searchCells, loc, true);
			var fromDest = PathSearch.FromPoint(self.World, mobile.Info, self, loc, targetPosition, true).Reverse();

			return pathFinder.FindBidiPath(fromSrc, fromDest);
		}
	}

	public class Move : Activity
	{
		static readonly List<CPos> NoPath = new List<CPos>();

		readonly IMobile mobile;
		readonly IEnumerable<IDisableMove> moveDisablers;
		readonly WRange nearEnough;
		readonly IPathCalculator pathCalculator;
		readonly IActor ignoredActor;

		List<CPos> path;
		CPos? destination;

		// For dealing with blockers
		bool isWaiting;
		bool hasNotifiedBlocker;
		int waitTicksRemaining;

		#region Constructors

		// Scriptable move order
		// Ignores lane bias and nearby units
		public Move(IActor self, CPos destination)
		{
			mobile = self.Trait<IMobile>();
			moveDisablers = self.TraitsImplementing<IDisableMove>();

			this.pathCalculator = new RegularPathFinder(self, mobile, destination);
			this.destination = destination;
			this.nearEnough = WRange.Zero;
		}

		// HACK: for legacy code
		public Move(IActor self, CPos destination, int nearEnough)
			: this(self, destination, WRange.FromCells(nearEnough)) { }

		public Move(IActor self, CPos destination, WRange nearEnough)
		{
			mobile = self.Trait<IMobile>();
			moveDisablers = self.TraitsImplementing<IDisableMove>();

			this.pathCalculator = new FindUnitPathfinder(self, mobile, destination);
			this.destination = destination;
			this.nearEnough = nearEnough;
		}

		// This constructor is not being used...
		public Move(IActor self, CPos destination, SubCell subCell, WRange nearEnough)
		{
			mobile = self.Trait<IMobile>();
			moveDisablers = self.TraitsImplementing<IDisableMove>();

			this.pathCalculator = new FindUnitRangefinder(self, mobile, destination, subCell, nearEnough);
			this.destination = destination;
			this.nearEnough = nearEnough;
		}

		public Move(IActor self, CPos destination, IActor ignoredActor)
		{
			mobile = self.Trait<IMobile>();
			moveDisablers = self.TraitsImplementing<IDisableMove>();

			this.pathCalculator = new RegularPathfinderWithIgnoredActor(self, mobile, destination, ignoredActor);

			this.destination = destination;
			this.nearEnough = WRange.Zero;
			this.ignoredActor = ignoredActor;
		}

		public Move(IActor self, IPathCalculator pathCalculator)
		{
			mobile = self.Trait<IMobile>();
			moveDisablers = self.TraitsImplementing<IDisableMove>();

			this.pathCalculator = pathCalculator;

			destination = null;
			nearEnough = WRange.Zero;
		}

		#endregion

		static int HashList<T>(List<T> xs)
		{
			var hash = 0;
			var n = 0;
			foreach (var x in xs)
				hash += n++ * x.GetHashCode();

			return hash;
		}

		// NOTE TO REMOVE: This previously used mobile.ToCell. actor.Location
		// and mobile.ToCell are exactly the same always, and using
		// actor location is more explicit and clearer in this context
		List<CPos> CalculatePath(IActor actor)
		{
			var path = this.pathCalculator.CalculatePath().TakeWhile(a => a != actor.Location).ToList();
			mobile.PathHash = HashList(path);
			return path;
		}

		public override Activity Tick(Actor self)
		{
			// Is the actor in its destination?
			if (destination == mobile.ToCell)
				return NextActivity;

			// Is the actor disabled?
			if (moveDisablers.Any(d => d.MoveDisabled(self)))
				return this;

			// If no path calculated, do it
			if (path == null)
			{
				if (mobile.TicksBeforePathing > 0)
				{
					--mobile.TicksBeforePathing;
					return this;
				}

				path = CalculatePath(self);
				SanityCheckPath();
			}

			// No more path. We've arrived to destination.
			if (path.Count == 0)
			{
				destination = mobile.ToCell;
				return this;
			}

			destination = path[0];

			var nextCell = NextCellToProcess(self);
			if (nextCell == null)
				return this;

			// Check if a turn is required. If it does,
			// then precede a Turn activity and then continue with the move
			var firstFacing = self.World.Map.FacingBetween(mobile.FromCell, nextCell.Value.First, mobile.Facing);
			if (firstFacing != mobile.Facing)
			{
				path.Add(nextCell.Value.First);
				return Util.SequenceActivities(new Turn(self, firstFacing), this);
			}

			// Start with the real movement. First set the movement intentions in mobile.
			mobile.SetLocation(mobile.FromCell, mobile.FromSubCell, nextCell.Value.First, nextCell.Value.Second);

			var move = new MoveFirstHalf(
				this,
				self.World.Map.CenterOfSubCell(mobile.FromCell, mobile.FromSubCell),
				Util.BetweenCells(self.World, mobile.FromCell, mobile.ToCell) + (self.World.Map.OffsetOfSubCell(mobile.FromSubCell) + self.World.Map.OffsetOfSubCell(mobile.ToSubCell)) / 2,
				mobile.Facing,
				mobile.Facing,
				0);

			return move;
		}

		#region For Testing
		public Activity Tick(IActor self)
		{
			// Is the actor disabled?
			if (moveDisablers.Any(d => d.MoveDisabled(self as Actor)))
				return this;

			// Is the actor in its destination?
			if (destination == mobile.ToCell)
				return NextActivity;

			// If no path calculated, do it
			if (path == null)
			{
				if (mobile.TicksBeforePathing > 0)
				{
					--mobile.TicksBeforePathing;
					return this;
				}

				path = CalculatePath(self);
				SanityCheckPath();
			}

			// No more path. We've arrived to destination.
			if (path.Count == 0)
			{
				destination = mobile.ToCell;
				return this;
			}

			destination = path[0];

			var nextCell = NextCellToProcess(self);
			if (nextCell == null)
				return this;

			// Check if a turn is required. If it does,
			// then precede a Turn activity and then continue with the move
			var firstFacing = self.World.Map.FacingBetween(mobile.FromCell, nextCell.Value.First, mobile.Facing);
			if (firstFacing != mobile.Facing)
			{
				path.Add(nextCell.Value.First);
				return Util.SequenceActivities(new Turn(self, firstFacing), this);
			}

			// Start with the real movement. First set the movement intentions in mobile.
			mobile.SetLocation(mobile.FromCell, mobile.FromSubCell, nextCell.Value.First, nextCell.Value.Second);

			var move = new MoveFirstHalf(
				this,
				self.World.Map.CenterOfSubCell(mobile.FromCell, mobile.FromSubCell),
				Util.BetweenCells(self.World, mobile.FromCell, mobile.ToCell) + (self.World.Map.OffsetOfSubCell(mobile.FromSubCell) + self.World.Map.OffsetOfSubCell(mobile.ToSubCell)) / 2,
				mobile.Facing,
				mobile.Facing,
				0);

			return move;
		}
		#endregion

		[Conditional("SANITY_CHECKS")]
		void SanityCheckPath()
		{
			if (path.Count == 0)
				return;
			var d = path[path.Count - 1] - mobile.ToCell;
			if (d.LengthSquared > 2)
				throw new InvalidOperationException("(Move) Sanity check failed");
		}

		// This function could be ported as an extension for IActor
		bool IsInRange()
		{
			var cellRange = nearEnough.Range / 1024;
			return (mobile.ToCell - destination.Value).LengthSquared <= cellRange * cellRange;
		}

		Pair<CPos, SubCell>? NextCellToProcess(IActor self)
		{
			// The only way that this conditional is true is if we come from a
			// MoveFirstHalf activity
			if (path.Count == 0)
				return null;

			var nextCell = path[path.Count - 1];

			if (!ShouldProcessCellNow(self, nextCell))
				return null;

			path.RemoveAt(path.Count - 1);

			var subCell = mobile.CheckAvailableSubCell(nextCell, SubCell.Any, ignoredActor);
			return Pair.New(nextCell, subCell);
		}

		bool ShouldProcessCellNow(IActor self, CPos nextCell)
		{
			// Next cell in the move is blocked by another actor
			// NOTE: Single-Responsibility Principle violation, this check should be up to the caller
			// NOTE2: Block path recalculation is quite expensive because of heuristics taking unnatural paths.
			// Think about it.
			if (mobile.CollidesWithOtherActorsInCell(nextCell, ignoredActor, true))
			{
				// Are we close enough? If so, then it's ok to consider that we have arrived.
				if (IsInRange())
				{
					path.Clear();
					return false;
				}

				// See if we have already notified any blockers on that
				// cell in previous ticks. If we haven't, do it.
				if (!hasNotifiedBlocker)
				{
					self.NotifyBlocker(nextCell);
					hasNotifiedBlocker = true;
				}

				// Wait a bit to see if they leave
				if (!isWaiting)
				{
					var info = self.TraitInfo<IMobileInfo>();
					waitTicksRemaining = info.WaitAverage + self.World.SharedRandom.Next(-info.WaitSpread, info.WaitSpread);
					isWaiting = true;
				}

				if (--waitTicksRemaining >= 0)
				{
					return false;
				}

				if (mobile.TicksBeforePathing > 0)
				{
					--mobile.TicksBeforePathing;
					return false;
				}

				// We've been waiting enough. Calculate a new path
				// NOTE: Is it necessary to do the Remove and then Add Influence if we are just calculating a path without side effects?
				// NOTE2: In order to prevent big path workarounds that could consume valuable processing power,
				// we could implement a maxDepthSearch variable into A*. See http://www.cokeandcode.com/main/tutorials/path-finding/
				mobile.RemoveInfluence();
				var newPath = CalculatePath(self);
				mobile.AddInfluence();

				if (newPath.Count != 0)
					path = newPath;

				return false;
			}

			hasNotifiedBlocker = false;
			isWaiting = false;
			return true;
		}

		public override void Cancel(Actor self)
		{
			path = NoPath;
			base.Cancel(self);
		}

		public override IEnumerable<Target> GetTargets(Actor self)
		{
			if (path != null)
				return Enumerable.Reverse(path).Select(c => Target.FromCell(self.World, c));
			if (destination != null)
				return new Target[] { Target.FromCell(self.World, destination.Value) };
			return Target.None;
		}

		// NOTE: This class is uncancellable until it completes its intended move...
		// If we could solve this, we could enable path smoothing!
		public abstract class MovePart : Activity
		{
			protected readonly Move Move;
			protected readonly int MoveFractionTotal;
			protected int moveFraction;
			readonly WPos from, to;
			readonly int fromFacing, toFacing;

			protected MovePart(Move move, WPos from, WPos to, int fromFacing, int toFacing, int startingFraction)
			{
				Move = move;
				this.from = from;
				this.to = to;
				this.fromFacing = fromFacing;
				this.toFacing = toFacing;
				moveFraction = startingFraction;
				MoveFractionTotal = (to - from).Length;
			}

			public override void Cancel(Actor self)
			{
				Move.Cancel(self);
				base.Cancel(self);
			}

			public override void Queue(Activity activity)
			{
				Move.Queue(activity);
			}

			public override Activity Tick(Actor self)
			{
				var mobile = self.Trait<IMobile>();
				var ret = InnerTick(self, Move.mobile);
				mobile.IsMoving = ret is MovePart;

				if (moveFraction > MoveFractionTotal)
					moveFraction = MoveFractionTotal;
				UpdateVisuals(self, mobile);

				return ret;
			}

			/// <summary>
			/// Function created for unit testing. Eventually, Activity base class should use this signature
			/// and therefore all activitie that inherit it should use the signature as well.
			/// </summary>
			public Activity Tick(IActor self)
			{
				var mobile = self.Trait<IMobile>();
				var ret = InnerTick(self, Move.mobile);
				mobile.IsMoving = ret is MovePart;

				if (moveFraction > MoveFractionTotal)
					moveFraction = MoveFractionTotal;
				UpdateVisuals(self, mobile);

				return ret;
			}

			Activity InnerTick(IActor self, IMobile mobile)
			{
				moveFraction += mobile.MovementSpeedForCell(self, mobile.ToCell);
				if (moveFraction <= MoveFractionTotal)
					return this;

				var next = OnComplete(self, mobile, Move);
				if (next != null)
					return next;

				return Move;
			}

			void UpdateVisuals(IActor self, IMobile mobile)
			{
				// avoid division through zero
				if (MoveFractionTotal != 0)
					mobile.SetVisualPosition(self, WPos.Lerp(from, to, moveFraction, MoveFractionTotal));
				else
					mobile.SetVisualPosition(self, to);

				if (moveFraction >= MoveFractionTotal)
					mobile.Facing = toFacing & 0xFF;
				else
					mobile.Facing = int2.Lerp(fromFacing, toFacing, moveFraction, MoveFractionTotal) & 0xFF;
			}

			protected abstract MovePart OnComplete(IActor self, IMobile mobile, Move parent);

			public override IEnumerable<Target> GetTargets(Actor self)
			{
				return Move.GetTargets(self);
			}
		}

		/// <summary>
		/// Performs the visual movement from a world position of a cell to another
		/// </summary>
		public class MoveFirstHalf : MovePart
		{
			public MoveFirstHalf(Move move, WPos from, WPos to, int fromFacing, int toFacing, int startingFraction)
				: base(move, from, to, fromFacing, toFacing, startingFraction) { }

			static bool IsTurn(IMobile mobile, CPos nextCell)
			{
				return nextCell - mobile.ToCell !=
					mobile.ToCell - mobile.FromCell;
			}

			protected override MovePart OnComplete(IActor self, IMobile mobile, Move parent)
			{
				var fromSubcellOffset = self.World.Map.OffsetOfSubCell(mobile.FromSubCell);
				var toSubcellOffset = self.World.Map.OffsetOfSubCell(mobile.ToSubCell);

				// This piece of code tries to emulate a "smooth" rotation
				// if the next point in the path will require rotation.
				var nextCell = parent.NextCellToProcess(self);
				if (nextCell != null)
				{
					if (IsTurn(mobile, nextCell.Value.First))
					{
						// This is planning another move between 2 cells. This responsibility
						// should be left to another class (maybe Move itself?)
						var nextSubcellOffset = self.World.Map.OffsetOfSubCell(nextCell.Value.Second);
						var ret = new MoveFirstHalf(
							Move,
							Util.BetweenCells(self.World, mobile.FromCell, mobile.ToCell) + (fromSubcellOffset + toSubcellOffset) / 2,
							Util.BetweenCells(self.World, mobile.ToCell, nextCell.Value.First) + (toSubcellOffset + nextSubcellOffset) / 2,
							mobile.Facing,
							Util.GetNearestFacing(mobile.Facing, self.World.Map.FacingBetween(mobile.ToCell, nextCell.Value.First, mobile.Facing)),
							moveFraction - MoveFractionTotal);

						// These two lines are doing exactly the same as mobile.SetPosition, except for setting the Visual Position.
						// Refactor!
						mobile.FinishedMoving(self);
						mobile.SetLocation(mobile.ToCell, mobile.ToSubCell, nextCell.Value.First, nextCell.Value.Second);
						return ret;
					}

					parent.path.Add(nextCell.Value.First);
				}

				// Start the seconf half of cell move, that, once completed,
				// will notify any finishMove events
				var ret2 = new MoveSecondHalf(
					Move,
					Util.BetweenCells(self.World, mobile.FromCell, mobile.ToCell) + (fromSubcellOffset + toSubcellOffset) / 2,
					self.World.Map.CenterOfCell(mobile.ToCell) + toSubcellOffset,
					mobile.Facing,
					mobile.Facing,
					moveFraction - MoveFractionTotal);

				// Raises an "event" telling subscribed units that this one is entering
				// TODO: Maybe replace this by a real event in the future?
				mobile.EnteringCell(self);

				// Set the unit as if it arrived to its destination
				mobile.SetLocation(mobile.ToCell, mobile.ToSubCell, mobile.ToCell, mobile.ToSubCell);
				return ret2;
			}
		}

		/// <summary>
		/// This class does the second part of a movement. Basically, it notifies
		/// any possible crush in the cell once movement is terminated.
		/// </summary>
		public class MoveSecondHalf : MovePart
		{
			public MoveSecondHalf(Move move, WPos from, WPos to, int fromFacing, int toFacing, int startingFraction)
				: base(move, from, to, fromFacing, toFacing, startingFraction) { }

			protected override MovePart OnComplete(IActor self, IMobile mobile, Move parent)
			{
				mobile.FinishedMoving(self);
				return null;
			}
		}
	}

	public static class ActorExtensionsForMove
	{
		public static bool IsMoving(this IActor self)
		{
			var a = self.GetCurrentActivity();
			if (a == null)
				return false;

			// HACK: Dirty, but it suffices until we do something better:
			if (a.GetType() == typeof(Move)) return true;
			if (a.GetType() == typeof(MoveAdjacentTo)) return true;
			if (a.GetType() == typeof(AttackMoveActivity)) return true;

			// Not a move:
			return false;
		}
	}
}
