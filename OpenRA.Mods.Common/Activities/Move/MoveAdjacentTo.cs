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
using System.Linq;
using OpenRA.Activities;
using OpenRA.Mods.Common.Pathfinder;
using OpenRA.Mods.Common.Traits;
using OpenRA.Traits;

namespace OpenRA.Mods.Common.Activities
{
	public class MoveAdjacentTo : Activity
	{
		static readonly List<CPos> NoPath = new List<CPos>();

		readonly IMobile mobile;
		readonly IPathFinder pathFinder;
		readonly DomainIndex domainIndex;
		readonly uint movementClass;

		protected ITarget Target { get; private set; }
		protected CPos targetPosition;
		Activity inner;
		bool repath;

		public MoveAdjacentTo(IActor self, ITarget target)
		{
			Target = target;

			mobile = self.Trait<IMobile>();
			pathFinder = self.World.WorldActor.Trait<IPathFinder>();
			domainIndex = self.World.WorldActor.Trait<DomainIndex>();
			movementClass = (uint)mobile.Info.GetMovementClass(self.World.TileSet);

			if (target.IsValidFor(self))
				targetPosition = self.World.Map.CellContaining(target.CenterPosition);

			repath = true;
		}

		protected virtual bool ShouldStop(IActor self, CPos oldTargetPosition)
		{
			return false;
		}

		protected virtual bool ShouldRepath(IActor self, CPos oldTargetPosition)
		{
			return targetPosition != oldTargetPosition;
		}

		protected virtual IEnumerable<CPos> CandidateMovementCells(IActor self)
		{
			return Util.AdjacentCells(self.World, Target);
		}

		public override Activity Tick(Actor self)
		{
			var targetIsValid = Target.IsValidFor(self);

			// Inner move order has completed.
			if (inner == null)
			{
				// We are done here if the order was cancelled for any
				// reason except the target moving.
				if (IsCanceled || !repath || !targetIsValid)
					return NextActivity;

				// Target has moved, and MoveAdjacentTo is still valid.
				inner = mobile.MoveTo(new SpecialCalculatePathToTarget(self, Target, mobile, targetPosition, domainIndex));
				repath = false;
			}

			if (targetIsValid)
			{
				// Check if the target has moved
				var oldTargetPosition = targetPosition;
				targetPosition = self.World.Map.CellContaining(Target.CenterPosition);

				var shouldStop = ShouldStop(self, oldTargetPosition);
				if (shouldStop || (!repath && ShouldRepath(self, oldTargetPosition)))
				{
					// Finish moving into the next cell and then repath.
					if (inner != null)
						inner.Cancel(self);

					repath = !shouldStop;
				}
			}
			else
			{
				// Target became invalid. Move to its last known position.
				Target = OpenRA.Traits.Target.FromCell(self.World, targetPosition);
			}

			// Ticks the inner move activity to actually move the actor.
			inner = Util.RunActivity(self, inner);

			return this;
		}

		public override IEnumerable<Target> GetTargets(Actor self)
		{
			if (inner != null)
				return inner.GetTargets(self);

			return OpenRA.Traits.Target.None;
		}

		public override void Cancel(Actor self)
		{
			if (inner != null)
				inner.Cancel(self);

			base.Cancel(self);
		}
	}
}
