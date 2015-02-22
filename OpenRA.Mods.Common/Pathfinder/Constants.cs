#region Copyright & License Information
/*
 * Copyright 2007-2015 The OpenRA Developers (see AUTHORS)
 * This file is part of OpenRA, which is free software. It is made
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation. For more information,
 * see COPYING.
 */
#endregion

namespace OpenRA.Mods.Common.Pathfinder
{
	public static class Constants
	{
		/// <summary>
		/// We can't rely on floating point math to be deterministic across all runtimes.
		/// The cases that use this will need to be changed to use integer math
		/// </summary>
		public const double Sqrt2 = 1.414;

		/// <summary>
		/// Min cost to arrive from once cell to an adjacent one
		/// (125 according to runtime tests where we could assess the cost
		/// a unit took to move one cell horizontally)
		/// </summary>
		public const int CellCost = 125;

		/// <summary>
		/// Min cost to arrive from once cell to a diagonal adjacent one
		/// (125 * Sqrt(2) according to runtime tests where we could assess the cost
		/// a unit took to move one cell diagonally)
		/// </summary>
		public const int DiagonalCellCost = 177;
	}
}
