using System;
using System.Collections.Generic;
using System.Linq;
using NativeCal.ViewModels;

namespace NativeCal.Helpers;

/// <summary>
/// Describes where a timed event should be placed in a multi-column overlap layout.
/// <see cref="ColumnIndex"/> is the 0-based column within the overlap cluster,
/// and <see cref="TotalColumns"/> is the width of the cluster.
/// </summary>
public readonly record struct TimedEventPlacement(CalendarEventViewModel Event, int ColumnIndex, int TotalColumns);

/// <summary>
/// Calculates non-overlapping column assignments for timed events within a single day.
/// Events that overlap in time are placed side-by-side in columns (like Google Calendar).
/// </summary>
public static class EventLayoutHelper
{
    /// <summary>
    /// Groups overlapping events into clusters and assigns each event a column
    /// within its cluster. Returns a list of <see cref="TimedEventPlacement"/> values
    /// that the view uses to calculate left offset and width.
    /// </summary>
    public static List<TimedEventPlacement> CalculateOverlapPlacements(IEnumerable<CalendarEventViewModel> sourceEvents)
    {
        var events = sourceEvents
            .OrderBy(e => e.StartTime)
            .ThenBy(e => e.EndTime)
            .ThenBy(e => e.Id)
            .ToList();

        if (events.Count == 0)
            return new List<TimedEventPlacement>();

        var placements = new List<TimedEventPlacement>();
        var clusters = new List<List<CalendarEventViewModel>>();

        List<CalendarEventViewModel>? currentCluster = null;
        DateTime clusterEnd = DateTime.MinValue;

        foreach (var evt in events)
        {
            if (currentCluster is null || evt.StartTime >= clusterEnd)
            {
                currentCluster = new List<CalendarEventViewModel> { evt };
                clusters.Add(currentCluster);
                clusterEnd = evt.EndTime;
                continue;
            }

            currentCluster.Add(evt);
            if (evt.EndTime > clusterEnd)
                clusterEnd = evt.EndTime;
        }

        foreach (var cluster in clusters)
        {
            var columnEnds = new List<DateTime>();
            var assignments = new List<(CalendarEventViewModel Event, int ColumnIndex)>();

            foreach (var evt in cluster)
            {
                int assignedColumn = -1;
                for (int column = 0; column < columnEnds.Count; column++)
                {
                    if (evt.StartTime >= columnEnds[column])
                    {
                        assignedColumn = column;
                        columnEnds[column] = evt.EndTime;
                        break;
                    }
                }

                if (assignedColumn < 0)
                {
                    assignedColumn = columnEnds.Count;
                    columnEnds.Add(evt.EndTime);
                }

                assignments.Add((evt, assignedColumn));
            }

            int totalColumns = Math.Max(columnEnds.Count, 1);
            placements.AddRange(assignments.Select(a => new TimedEventPlacement(a.Event, a.ColumnIndex, totalColumns)));
        }

        return placements;
    }
}
