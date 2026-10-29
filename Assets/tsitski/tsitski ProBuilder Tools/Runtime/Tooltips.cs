namespace Tsitski.ProBuilderTools {
    internal static class Tooltips {
        public const string PathEditing =
            "Edited in the Scene view. Click a point to select it, Shift+click or Shift+drag to select several. Selected points follow the Move / Rotate / Scale tool. " +
            "Click an edge midpoint to insert a point, click the dot past an open end to extend the path. " +
            "Delete removes selected points, Ctrl/Cmd+C and Ctrl/Cmd+V copy and paste them, Esc clears the selection.";

        public const string Radii = "Corner radius per point. Select points and drag the cyan handle on a corner to round it.";
        public const string Regenerated = " The mesh is regenerated, so ProBuilder edits to it are overwritten.";

        public const string ArrayStats =
            "Shared settings. Also holds a prefab source, the source mesh when Store Source is on, and the path when Store Path is on.";
        public const string ArraySource =
            "Model it with the normal ProBuilder tools, it's only shown while selected. Its +Z points along the path and +Y up, its rotation and scale carry over to every copy.";
        public const string ArrayStatsSource = "Prefab source shared by every array using these stats.";
        public const string ArrayPillarStats =
            "Place a separate pillar object built from these Pillar Stats at every point instead of copying the source mesh. Leave empty to use the source mesh.";
        public const string ArrayPillarHeight = "Height of each pillar when copying a pillar from Pillar Stats.";
        public const string MergeIntoMesh =
            "Combine the pillars into this array's own mesh instead of placing separate pillar objects. Snapping still applies to each pillar before merging.";
        public const string StoreSource =
            "Store the source mesh in the stats. Every array using them copies it, and editing an array's own source saves into the stats.";
        public const string Spacing = "Distance between copies along the path.";
        public const string Offset = "Moves the first copy along each division (or run) when Fit Spacing is off.";
        public const string FitSpacing =
            "Adjust the spacing inside each division (or run) so copies land evenly from end to end. Off keeps the exact Spacing, starting at Offset.";
        public const string SplitAtCorners =
            "Treat each straight run between corners (turns over 15 degrees) on its own, so spacing restarts at every corner and corners always get a slot.";
        public const string DivisionLength =
            "Split every run into equal divisions as close to this length as fits (0 = no divisions), and space copies inside each division. " +
            "Match it with another array's spacing to fill the gaps between its copies.";
        public const string SkipCorners = "Leave out the copies that land on corners (and the ends of an open path).";
        public const string SkipDivisions = "Leave out the copies that land on division points, for example where another array's pillars stand.";
        public const string AlignToPath = "Rotate each copy to follow the path direction.";
        public const string RoundingSegments = "How smooth rounded corners are.";
        public const string ArrayPath = "Path the copies are placed along. " + PathEditing + Regenerated;
        public const string Closed = "Connect the last point back to the first.";
        public const string StorePath = "Store the path in the stats, editing it updates everything using the asset.";

        public const string TrimStats = "Shared profile and settings. With Store Path on it also holds the path (points, radii, Closed).";
        public const string Profile =
            "Cross-section: X points to the right of the path, Y points up, (0, 0) sits on the path. " +
            "Edited in the Scene view at the start of the path, the same way as the path's points (with Trim Stats assigned, this edits the asset).";
        public const string TrimPath = "Path the profile is swept along. " + PathEditing + Regenerated;
        public const string CapEnds = "Close the ends of an open trim.";
        public const string SmoothingAngle = "Faces meeting at less than this angle are shaded smooth.";
        public const string LoopCutStep = "Distance between loop cuts along each segment, smaller means more cuts. Below 0.1 turns them off.";
        public const string RoundBevel = "Segments used to round each path corner, 0 keeps sharp mitred corners. The profile itself is unchanged.";
        public const string FlattenAngledEnds =
            "On sloped ends of an open path, run the trim level for a short way before the slope starts, so the end caps are upright instead of tilted with the slope.";
        public const string ProfileMirror =
            "Draw half the profile and it's copied and flipped across the Y axis (X becomes -X), the X axis (Y becomes -Y), or both. Only the drawn half is edited in the Scene view.";

        public const string Floor =
            "When set, the Path is generated from the outline of this floor's top faces (the highest faces pointing up) and the walls rebuild when it's edited." + Regenerated;
        public const string FloorPlanPath =
            "Path the walls are built along, in local space. Generated from the Floor when one is assigned, otherwise " + PathEditing + Regenerated;
        public const string WallHeight = "Height of the walls.";
        public const string WallThickness = "Thickness of the walls.";
        public const string Alignment =
            "Inside keeps the wall's outer face flush with the floor edge, Outside puts the wall's inner face there, Center straddles it.";
        public const string WallsAroundHoles = "Also build walls along holes in the floor.";
        public const string SquareCorners =
            "Put a separate block where two walls meet, with the walls ending square against it, instead of a mitred joint.";
        public const string VerticalLoopCutStep = "Distance between horizontal loop cuts up the walls, smaller means more cuts. Below 0.1 turns them off.";
        public const string FloorPlanStats = "Shared Height, Thickness, Alignment, corner and loop cut settings, editing them updates every floor plan using the asset.";

        public const string PolygonStats = "Shared Thickness, and the outline and holes when Save Polygon is on, editing them updates every polygon using the asset.";
        public const string PolygonThickness = "How far the polygon is extruded up from its outline.";
        public const string SavePolygon = "Store the outline and holes in the stats, editing them updates every polygon using the asset.";
        public const string PolygonPath =
            "Outline of the polygon on the XZ plane, in local space. " + PathEditing + Regenerated;
        public const string PolygonHoles =
            "Holes cut through the polygon. Overlapping holes are merged, and holes crossing the outline cut notches out of it.";
        public const string PolygonHolePath = "Outline of the hole on the XZ plane, in local space. " + PathEditing;

        public const string StairsStats = "Shared Width, Step Height, Solid, Thickness and cutter settings, editing them updates every stairs using the asset.";
        public const string StairsPath =
            "Each pair of points is a flight. The first point is the front edge of the first step, the last the back edge of the last step, " +
            "every point in between gets a Width by Width landing at its height. " + PathEditing + Regenerated;
        public const string StairsRadii =
            "Rounding a point replaces its landing with winding steps, the radius is the inner edge's radius. " +
            "Flights joined by rounded points climb evenly between landings, so a rounded point's own height is ignored.";
        public const string StairsWidth = "Width of the stairs and landings.";
        public const string StepHeight = "Target riser height, the step count is rounded so every step fits exactly.";
        public const string Solid = "Fill the stairs down to the lowest path point, otherwise they get a sloped underside Thickness thick.";
        public const string StairsThickness = "Thickness of the underside and landings when Solid is off.";
        public const string GenerateCutter =
            "Add a \"Stairs Cutter\" child, a flat Path cutter following the stairs, so floors above get a stairwell cut through them. " +
            "It's rebuilt with the stairs, so don't edit it by hand.";
        public const string CutterHeadroom =
            "Height below the top of the stairs the cutter covers. It only runs along the part of the stairs above that height, and reaches down to it (never into the bottom floor).";

        public const string LeftTrim =
            "Trim Stats for a trim generated along the left edge of the stairs and landings. Its profile's X points away from the stairs, Y up, " +
            "(0, 0) sits on the line through the back edges of the steps.";
        public const string RightTrim =
            "Trim Stats for a trim generated along the right edge of the stairs and landings. Its profile's X points away from the stairs, Y up, " +
            "(0, 0) sits on the line through the back edges of the steps.";

        public const string PillarStats = "Shared profile, middle split, sides and smoothing, editing them updates every pillar using the asset.";
        public const string PillarHeight = "Total height of the pillar. The middle part repeats to fill it, drag the handle on top of the pillar in the Scene view.";
        public const string BottomProfile =
            "Outline of the pillar's base, revolved around its axis (or swept along a corner). X is the distance from the axis, Y runs 0 to 1 across the bottom section's height.";
        public const string CenterProfile =
            "Outline of the middle part, repeated or stretched to fill the space between the bottom and top sections. X is the distance from the axis, Y runs 0 to 1 across one repeat.";
        public const string TopProfile =
            "Outline of the pillar's cap. X is the distance from the axis, Y runs 0 to 1 across the top section's height.";
        public const string PillarSectionPoints =
            "Points of this section, edited in the Scene view the same way as path points. X is the distance from the axis, Y is 0 to 1 across the section.";
        public const string BottomHeight =
            "Height of the bottom section in metres. Its profile is stretched to fit, and squished if the pillar is shorter than the bottom and top sections together.";
        public const string TopHeight =
            "Height of the top section in metres. Its profile is stretched to fit, and squished if the pillar is shorter than the bottom and top sections together.";
        public const string CenterHeight =
            "Height of one repeat of the center profile in metres. The repeat count is rounded so whole repeats fill the space between the bottom and top sections.";
        public const string StretchCenter = "Stretch the middle part once to fill the space between the bottom and top parts instead of repeating it.";
        public const string PillarSides = "Number of sides. 4 makes a square pillar with its faces along X and Z.";
        public const string PillarShape =
            "Round revolves the profile around the pillar's axis. Corner sweeps it along two legs (start to center to end) " +
            "so it wraps the corner of a wall, the profile's X pointing away from the wall.";
        public const string CornerSide =
            "Which side of the legs the pillar is on, going from the start leg through the corner to the end leg: Outside on the right, Inside on the left. " +
            "Snapped and generated pillars follow the path's direction, so Outside is a floor plan's exterior face and Inside its interior face at every corner.";
        public const string FloorPlanOutlineSettings =
            "Trims and arrays generated as child objects along the wall faces, one per entry and face. " +
            "Use Array Stats with corner Pillar Stats and Auto Snap To Path to put pillars in every corner.";
        public const string OutlineTrimStats = "Trim Stats for a trim running along the wall face. Its profile's X points away from the wall, Y up.";
        public const string OutlineArrayStats = "Array Stats for an array running along the wall face.";
        public const string OutlineWallSide =
            "Which wall faces get the trim and array: Inside faces the floor plan's interior, Outside faces away from it, or Both.";
        public const string OutlineHeight = "Height of the trim and array path, measured up from the bottom of the walls (or down from the top).";
        public const string HeightFromTop = "Measure Height down from the top of the walls instead of up from the bottom.";
        public const string ArrayCornerSide =
            "Corner Side given to the pillars this array places. Floor plans set it per wall face: Outside for exterior faces, Inside for interior ones.";
        public const string PillarRoundBevel =
            "Segments used to round the corner, 0 keeps a sharp mitre. Only pillars on the outer (convex) side of the bend are rounded, ones sitting in the inner side stay mitred.";
        public const string CornerLength = "Length of both legs, from the center point to the start and end points.";
        public const string CornerStart = "Point the first leg runs towards from the pillar's origin (the corner). Only its direction is used, the length comes from Corner Length.";
        public const string CornerEnd = "Point the second leg runs towards from the pillar's origin (the corner). Only its direction is used, the length comes from Corner Length.";
        public const string GenerateDownwards =
            "Build the pillar hanging down from its origin (the top of the pillar sits at the object's position) instead of standing up from it.";
        public const string SnapToFlats =
            "When no path corner is in range, snap onto the nearest straight part of a path instead. " +
            "A corner pillar lies flat against the wall (both legs along it): Outside on the face away from a floor plan's interior, Inside on the face toward it.";
        public const string AutoSnapToPath =
            "While moving the pillar, snap its position to the nearest corner of a floor plan, trim, array or stairs path; corner pillars also line their legs up with the path. " +
            "Floor plan corners snap to the wall face on the pillar's side.";
        public const string SnapDistance = "How close the pillar's position has to be to a path corner to snap to it.";

        public const string SmoothCollider =
            "Give the MeshCollider a ramp running from the front edge of the first step to the back edge of the last, instead of the steps, so characters walk up smoothly.";

        public const string CutterStats = "Shared cutter settings. Every cutter using the asset copies all its settings, and editing one of them edits the asset.";
        public const string Shape = "Cutting shape. Box, Cylinder, Polygon and Explosion are outlines on XY extruded along Z, Path sweeps a box along points.";
        public const string CornerRadius = "Rounds the box's corners.";
        public const string CornerSegments = "How smooth rounded corners are.";
        public const string CylinderSegments = "Number of sides.";
        public const string ExplosionSeed = "Random seed for the explosion shape.";
        public const string ExplosionSpikes = "Number of main spikes.";
        public const string ExplosionMinorSpikes = "Small spikes between each pair of main spikes.";
        public const string ExplosionJaggedness = "How irregular the spikes are.";
        public const string PolygonPoints =
            "Outline drawn on the XY plane and extruded along Z. Edited in the Scene view: click a point to select it, Shift+click or Shift+drag to select several, " +
            "selected points follow the Move / Rotate / Scale tool. Click an edge midpoint to insert a point. " +
            "Delete removes selected points, Ctrl/Cmd+C and Ctrl/Cmd+V copy and paste them, Esc clears the selection.";
        public const string CutterPath = "Path the box is swept along, centred on it, in the cutter's local space. " + PathEditing;
        public const string PathRotations =
            "Rotation of the swept box at each path point. Select points and use the Rotate tool to tilt or roll it.";
        public const string PathWidth = "Width of the swept box.";
        public const string PathHeight = "Height of the swept box.";
        public const string CutterCenter = "Centre of the cutter's box in local space.";
        public const string CutterSize = "Size of the cutter's box in local space.";
        public const string CreateMesh = "Fill the inside of the hole with faces.";
        public const string CutMode = "Which meshes this cutter cuts: ProBuilder meshes, regular meshes (any MeshFilter with a saved mesh, including generated ones like fences), or both. Regular meshes are never modified, the cut result is generated next to them.";
        public const string CutThroughParent = "Also cut the mesh this cutter is a child of, and the other meshes under it.";
        public const string IgnorePrefabMeshes = "Don't cut meshes that are part of a prefab instance (anything under the outermost prefab root).";
        public const string GenerateFaces ="Which of the cutter's sides get faces inside the hole.";
        public const string ClipToTunnel = "Trim the hole's faces to the mesh along these axes.";
        public const string RevealMaterial = "Material for the faces inside the hole, defaults to the mesh's first material.";

        public const string AutoNameGenerators = "Name generator objects (cutters, trims, arrays, stairs, pillars, floor plans) after their stats asset, or after the component when they have no stats.";
        public const string BakedCutters = "Cutters already baked into this mesh. They no longer cut it; remove one to let it cut again.";
        public const string ContributeGIByDefault = "Turn on the Contribute GI static flag on objects when a generator component is added to them (polygons, floor plans, trims, arrays, stairs, pillars) and on the child objects generators create.";
        public const string GizmosOnlyOnSelected = "Only draw cutter gizmos for selected cutters.";
        public const string RebuildAfterDragging = "Wait until a dragged handle is released before rebuilding: recutting meshes around a cutter (box, polygon points, path) and rebuilding trims, arrays, stairs, pillars and floor plans from their paths and profiles. Turn off to rebuild live while dragging.";
        public const string RoofStats = "Shared profile and thickness, and the paths when Save Paths is on, editing them updates every roof using the asset.";
        public const string RoofProfile =
            "Shape of the roof slope from the eave (first point) to the ridge (last point), stretched to every path's half width and height. " +
            "Two points give a plain pitch, more give mansard or curved roofs. Edit it in the Scene view on any path.";
        public const string RoofThickness = "Thickness of the roof shell. The top is copied down by this much, so the attic underneath stays open.";
        public const string SaveRoofPaths = "Store the paths in the stats, editing them updates every roof using the asset.";
        public const string RoofPaths =
            "Center lines of the building's wings, each with its own width and height. Overlapping or touching wings are merged into one roof with valleys where they meet.";
        public const string RoofPathWidth = "Width of the roof around this path, from eave to eave. Drag the profile's eave point or the dot opposite it in the Scene view.";
        public const string RoofPathHeight = "Height of this path's ridge above the eaves. Drag the profile's ridge point in the Scene view.";
        public const string RoofGableStart = "End the roof at the first point with a vertical gable instead of a hip. Click the end marker in the Scene view.";
        public const string RoofGableEnd = "End the roof at the last point with a vertical gable instead of a hip. Click the end marker in the Scene view.";
        public const string RoofPathPoints = "Center line on the XZ plane, in local space. " + PathEditing;
        public const string DefaultMeshMaterial = "Material given to generated meshes (polygons, roofs, trims, arrays, stairs, pillars, floor plan walls) when they don't have one.";
    }
}
