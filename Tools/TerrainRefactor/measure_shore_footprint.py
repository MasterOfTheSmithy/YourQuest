"""Measure uncovered wet area in retained native-shore witness cells, without editing a plan."""
import argparse
import hashlib
import json
import math
from datetime import datetime, timezone
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
EVIDENCE = ROOT / 'outputs/TerrainRefactor_20261011'
EPSILON = 1e-9


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def cross(a, b, p):
    return (b[0] - a[0]) * (p[1] - a[1]) - (b[1] - a[1]) * (p[0] - a[0])


def area(polygon):
    # note: Translate before multiplying to retain small-cell precision at regional coordinates.
    if len(polygon) < 3:
        return 0.0
    origin = polygon[0]
    return sum(cross(origin, polygon[i], polygon[i + 1]) for i in range(1, len(polygon) - 1)) * .5


def ccw(polygon):
    return polygon if area(polygon) >= 0 else list(reversed(polygon))


def clip_half_plane(polygon, a, b, inside):
    result = []
    if not polygon:
        return result
    for previous, current in zip(polygon[-1:] + polygon[:-1], polygon):
        first, second = cross(a, b, previous), cross(a, b, current)
        first_in = first >= 0 if inside else first <= 0
        second_in = second >= 0 if inside else second <= 0
        if first_in != second_in:
            t = first / (first - second)
            result.append(tuple(previous[i] + (current[i] - previous[i]) * t for i in range(2)))
        if second_in:
            result.append(current)
    return result


def subtract(polygon, cover):
    # note: Successive outside pieces partition the subject; overlapping cover polygons cannot double-count covered area.
    pieces = []
    remaining = polygon
    cover = ccw(cover)
    if abs(area(cover)) <= EPSILON:
        return [polygon]
    for a, b in zip(cover, cover[1:] + cover[:1]):
        if a == b:
            continue
        outside = clip_half_plane(remaining, a, b, False)
        if abs(area(outside)) > EPSILON:
            pieces.append(outside)
        remaining = clip_half_plane(remaining, a, b, True)
        if abs(area(remaining)) <= EPSILON:
            break
    return pieces


def wet_polygon(points, heights, level):
    result = []
    for i in range(3):
        j = (i + 1) % 3
        first, second = heights[i] < level, heights[j] < level
        if first:
            result.append(points[i])
        if first != second:
            t = (level - heights[i]) / (heights[j] - heights[i])
            result.append(tuple(points[i][k] + (points[j][k] - points[i][k]) * t for k in range(2)))
    return ccw(result)


def triangle_indices(triangle, resolution, version):
    cell, side = divmod(triangle, 2)
    z, x = divmod(cell, resolution - 1)
    base = z * resolution + x
    if version == 1:
        return (base, base + resolution, base + 1) if side == 0 else (base + 1, base + resolution, base + resolution + 1)
    return (base, base + resolution + 1, base + 1) if side == 0 else (base, base + resolution, base + resolution + 1)


def lake_polygons(water):
    domain = water['connectedDomain']
    resolution = domain['resolution']
    bed = domain['bed']
    for triangle in domain['wetTriangles']:
        indices = triangle_indices(triangle, resolution, domain['version'])
        points = [(domain['minimumX'] + vertex % resolution * domain['spacing'],
                   domain['minimumZ'] + vertex // resolution * domain['spacing']) for vertex in indices]
        polygon = wet_polygon(points, [bed[vertex] for vertex in indices], water['waterLevelNormalized'])
        if abs(area(polygon)) > EPSILON:
            yield polygon


def channel_polygons(water):
    points = water['controlPoints']
    sides = []
    for i, point in enumerate(points):
        previous, following = points[max(0, i - 1)], points[min(len(points) - 1, i + 1)]
        dx, dz = following['x'] - previous['x'], following['z'] - previous['z']
        length = math.hypot(dx, dz)
        # note: The prepared production sampler uses forward for degenerate control tangents.
        side = (-dz / length, dx / length) if length * length >= .0001 else (-1.0, 0.0)
        radius = max(.75, max(water['nominalWidth'], point['width']) * .5)
        center = (point['x'], point['z'])
        sides.append((tuple(center[k] - side[k] * radius for k in range(2)),
                      tuple(center[k] + side[k] * radius for k in range(2))))
    for (al, ar), (bl, br) in zip(sides, sides[1:]):
        yield ccw([al, bl, ar])
        yield ccw([ar, bl, br])


def bounds(polygon):
    return (min(p[0] for p in polygon), min(p[1] for p in polygon),
            max(p[0] for p in polygon), max(p[1] for p in polygon))


def overlaps(first, second):
    return first[0] <= second[2] and second[0] <= first[2] and first[1] <= second[3] and second[1] <= first[3]


def contains(polygon, point):
    return all(cross(a, b, point) >= -EPSILON for a, b in zip(polygon, polygon[1:] + polygon[:1]))


def distance(polygon, point):
    if contains(polygon, point):
        return 0.0
    best = math.inf
    for a, b in zip(polygon, polygon[1:] + polygon[:1]):
        dx, dz = b[0] - a[0], b[1] - a[1]
        length_squared = dx * dx + dz * dz
        t = max(0, min(1, ((point[0] - a[0]) * dx + (point[1] - a[1]) * dz) / length_squared)) if length_squared else 0
        best = min(best, math.hypot(point[0] - a[0] - dx * t, point[1] - a[1] - dz * t))
    return best


def self_checks():
    square = [(0, 0), (2, 0), (2, 2), (0, 2)]
    cases = [
        ('disjoint', [[(3, 0), (4, 0), (4, 2), (3, 2)]], 4),
        ('full-cover', [square], 0),
        ('half-cover', [[(0, 0), (1, 0), (1, 2), (0, 2)]], 2),
        ('interior-hole', [[(.5, .5), (1.5, .5), (1.5, 1.5), (.5, 1.5)]], 3),
        ('overlapping-covers', [[(0, 0), (1.5, 0), (1.5, 2), (0, 2)], [(1, 0), (2, 0), (2, 2), (1, 2)]], 0),
        ('reversed-cover', [list(reversed(square))], 0),
        ('duplicate-cover-vertex', [[(0, 0), (0, 0), (2, 0), (2, 2), (0, 2)]], 0),
        ('degenerate-cover', [[(0, 0), (1, 0), (2, 0)]], 4),
    ]
    passed = []
    for name, covers, expected in cases:
        pieces = [square]
        for cover in covers:
            pieces = [remainder for piece in pieces for remainder in subtract(piece, cover)]
        assert abs(sum(abs(area(piece)) for piece in pieces) - expected) < 1e-8, name
        passed.append(name)
    triangle = [(0, 0), (2, 0), (0, 2)]
    assert abs(area(wet_polygon(triangle, [0, 2, 2], 1)) - .5) < 1e-8
    passed.append('known-linear-level-area')
    assert distance(square, (1, 1)) == 0 and abs(distance(square, (3, 1)) - 1) < 1e-8
    passed.append('known-nearest-edge-distance')
    moved = [(x + 1_000_000, z - 1_000_000) for x, z in square]
    assert area(moved) == 4
    passed.append('translated-small-cell-area')
    return passed


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--native', required=True, type=Path)
    parser.add_argument('--out', required=True, type=Path)
    args = parser.parse_args()
    checks = self_checks()
    native = args.native.resolve()
    output = args.out.resolve()
    if not native.is_relative_to(EVIDENCE.resolve()) or not output.is_relative_to(EVIDENCE.resolve()) or output.exists():
        raise ValueError('Inputs and a fresh output directory must belong to this terrain-refactor evidence workspace')
    receipt_path = native / 'Receipt.json'
    receipt = json.loads(receipt_path.read_text())
    if receipt['error'] or not receipt['inputsUnchanged']:
        raise ValueError('Native measurement requires completed unchanged inputs, even when the physical gate fails')
    artifact = (ROOT / receipt['input']).resolve()
    if digest(artifact) != receipt['inputSha256']:
        raise ValueError('Native draft identity changed')
    inputs = {str(receipt_path): digest(receipt_path), str(artifact): digest(artifact), str(Path(__file__)): digest(Path(__file__))}
    for path, expected in receipt['sourceSha256'].items():
        if digest(ROOT / path) != expected:
            raise ValueError('Native measurement source changed: ' + path)
        inputs[str(ROOT / path)] = expected
    runtime = ROOT / 'Library/ScriptAssemblies/Assembly-CSharp.dll'
    if digest(runtime) != receipt['runtimeAssemblySha256']:
        raise ValueError('Native measurement runtime assembly changed')
    inputs[str(runtime)] = receipt['runtimeAssemblySha256']
    plan = json.loads(artifact.read_text())
    waters = plan['spatialPlanV2']['blueprint']['hydrology']
    if any(w['hasConnectedDomain'] and w['connectedDomain']['version'] not in (4, 5) for w in waters):
        raise ValueError('This footprint comparison requires version-pinned canonical materialized banks')
    covers = [polygon for water in waters if water['hasConnectedDomain'] for polygon in lake_polygons(water)]
    covers += [polygon for water in waters if water['kind'] in (1, 5) for polygon in channel_polygons(water)
               if abs(area(polygon)) > EPSILON]
    cover_bounds = [bounds(polygon) for polygon in covers]
    results = []
    witnesses = []
    for index, water in enumerate(waters):
        if not water['hasConnectedDomain']:
            continue
        source = native / ('water-' + str(index) + '-samples.json')
        inputs[str(source)] = digest(source)
        samples = json.loads(source.read_text())
        cells = {}
        for sample in samples:
            corners = sample['cellSamples']
            if len(corners) == 4:
                key = (corners[0]['x'], corners[0]['z'])
                if key in cells and cells[key] != corners:
                    raise ValueError('Inconsistent retained native witness cell')
                cells[key] = corners
        uncovered_area = 0.0
        maximum_gap = 0.0
        uncovered_cells = 0
        for key, corners in sorted(cells.items()):
            wet = []
            # note: The independently measured native terrain diagonal is LL-UR; these are source-node polygons, not a new terrain authority.
            for indices in ((0, 3, 1), (0, 2, 3)):
                polygon = wet_polygon([(corners[i]['x'], corners[i]['z']) for i in indices],
                                      [corners[i]['sourceHeight'] for i in indices], water['waterLevelNormalized'] * 140)
                if abs(area(polygon)) > EPSILON:
                    wet.append(polygon)
            cell_bounds = bounds([(p['x'], p['z']) for p in corners])
            candidates = [cover for cover, box in zip(covers, cover_bounds) if overlaps(box, cell_bounds)]
            remainder = wet
            for cover in candidates:
                remainder = [piece for polygon in remainder for piece in subtract(polygon, cover)]
                if not remainder:
                    break
            missing = sum(abs(area(polygon)) for polygon in remainder)
            if missing <= 1e-7:
                continue
            uncovered_cells += 1
            uncovered_area += missing
            probes = [point for polygon in remainder for point in polygon]
            probes += [tuple(sum(point[k] for point in polygon) / len(polygon) for k in range(2)) for polygon in remainder]
            # note: Include the complete accepted footprint when measuring nearest distance, even when its nearest edge lies outside this cell.
            worst = max((min(distance(cover, point) for cover in covers), point) for point in probes)
            maximum_gap = max(maximum_gap, worst[0])
            witnesses.append(dict(waterIndex=index, cell=list(key), uncoveredAreaSquareMetres=missing,
                                  maximumWitnessGapMetres=worst[0], worstPoint=list(worst[1]), uncoveredPolygons=remainder))
        results.append(dict(hydrologyId=water['hydrologyId'], retainedShoreSamples=len(samples), retainedCells=len(cells),
                            uncoveredCells=uncovered_cells, uncoveredAreaSquareMetres=uncovered_area,
                            maximumWitnessFootprintGapMetres=maximum_gap))
    unchanged = all(digest(Path(path)) == expected for path, expected in inputs.items())
    assert unchanged
    output.mkdir(parents=True)
    (output / 'Witnesses.json').write_text(json.dumps(witnesses, indent=2) + '\n')
    result = dict(utc=datetime.now(timezone.utc).isoformat(), selfChecks=checks, measurements=results,
                  sourceNativeReceipt=str(receipt_path), nativeGatePassed=receipt['passed'], inputsUnchanged=unchanged,
                  inputSha256=inputs, fullGoalStatus='INCOMPLETE',
                  evidence='READ_ONLY_PLANAR_DIFFERENCE_OF_RETAINED_SOURCE_NODE_WET_TRIANGLES_AND_ACCEPTED_WATER_FOOTPRINTS',
                  limits='Only captured failing nominal-shore cells, not a full domain scan. Source node heights precede native quantization. All accepted lake, river and waterfall footprints count as cover regardless of surface elevation, making uncovered area a conservative witness. Double-precision geometric reconstruction is diagnostic, not a replacement canonical sampler. No final grading, collider, waves, LOD, streaming, visual or travel acceptance.')
    (output / 'Receipt.json').write_text(json.dumps(result, indent=2) + '\n')
    print(json.dumps(result))


if __name__ == '__main__':
    main()
