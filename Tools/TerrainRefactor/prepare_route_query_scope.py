"""Prepare an invocation-local accepted route query without rewriting source encoding."""
from pathlib import Path
import difflib, hashlib, json

root=Path(__file__).resolve().parents[2]
out=root/'outputs/TerrainRefactor_20261014_StreamingRetest/ScopedRouteQueries'
out.mkdir(exist_ok=True)
targets=['YQGeneratedWorldEnvironment.cs','YQContinuousWorldFeatureAuthority.cs']
sources={name:(root/'Assets/Assets/Scripts/Generated'/name).read_bytes() for name in targets}
def digest(data): return hashlib.sha256(data).hexdigest()
def text(data):
    try:return data.decode('utf-8')
    except UnicodeDecodeError:return data.decode('cp1252')
old='''    internal static List<Vector2> GetAcceptedRouteCenterline(YQPreparedSpatialMaterializationV2 prepared, int routeIndex)
    {
        // note: Bridge analysis and physical validation consume the existing road projection, including its site gates, skirts and origin landmark detour.
        var paths = new List<LivedPathSegment>();
        BuildV2LivedPathNetwork(paths, prepared, null, null, false);
        string id = prepared.GetRoute(routeIndex).routeId;
        var points = new List<Vector2>();
        foreach (var path in paths)
        {
            if (path.routeId != id) continue;
            if (points.Count == 0) points.Add(path.start);
            else if ((points[points.Count - 1] - path.start).sqrMagnitude > .01f)
                throw new InvalidOperationException("Accepted road projection is disconnected: " + id);
            points.Add(path.end);
        }
        return points;
    }
'''
new='''    internal static List<Vector2> GetAcceptedRouteCenterline(YQPreparedSpatialMaterializationV2 prepared, int routeIndex)
    {
        // note: Single-route callers retain their independent fresh projection; multi-route callers may own one immutable-query scope.
        return new AcceptedRouteCenterlineQueries(prepared).GetRoute(routeIndex);
    }

    internal sealed class AcceptedRouteCenterlineQueries
    {
        private readonly YQPreparedSpatialMaterializationV2 prepared;
        private readonly List<LivedPathSegment> paths = new List<LivedPathSegment>();

        internal AcceptedRouteCenterlineQueries(YQPreparedSpatialMaterializationV2 prepared)
        {
            // note: This invocation-local snapshot uses the same exact immutable projection and excludes POI spurs; no terrain, plan, cache or random authority is retained.
            this.prepared = prepared;
            BuildV2LivedPathNetwork(paths, prepared, null, null, false);
        }

        internal List<Vector2> GetRoute(int routeIndex)
        {
            // note: Preserve original segment order, duplicate station handling, discontinuity rejection and fresh result-list ownership.
            string id = prepared.GetRoute(routeIndex).routeId;
            var points = new List<Vector2>();
            foreach (var path in paths)
            {
                if (path.routeId != id) continue;
                if (points.Count == 0) points.Add(path.start);
                else if ((points[points.Count - 1] - path.start).sqrMagnitude > .01f)
                    throw new InvalidOperationException("Accepted road projection is disconnected: " + id);
                points.Add(path.end);
            }
            return points;
        }
    }
'''
data=sources[targets[0]]
newline=b'\n' if old.encode('ascii') in data else b'\r\n'
def encoded(value):return value.encode('ascii').replace(b'\n',newline)
assert data.count(encoded(old))==1
proposals={targets[0]:data.replace(encoded(old),encoded(new))}
data=sources[targets[1]]
newline=b'\n' if b'        if (!UsesNativeBridgeRoutes(prepared)) return result;\n' in data else b'\r\n'
anchor=b'        if (!UsesNativeBridgeRoutes(prepared)) return result;'+newline
assert data.count(anchor)==1
data=data.replace(anchor,anchor+encoded('        // note: Lazily project the immutable road network once for all endpoint queries in this constructor; an empty crossing set still does no road work.\n        YQGeneratedWorldEnvironment.AcceptedRouteCenterlineQueries routeQueries = null;\n'))
before=b'points ??= YQGeneratedWorldEnvironment.GetAcceptedRouteCenterline(prepared, routeIndex);'
assert data.count(before)==1
data=data.replace(before,b'points ??= (routeQueries ??= new YQGeneratedWorldEnvironment.AcceptedRouteCenterlineQueries(prepared)).GetRoute(routeIndex);')
proposals[targets[1]]=data
rows=[]; patch=[]
for name in targets:
    (out/(name+'.before')).write_bytes(sources[name])
    (out/(name+'.proposed')).write_bytes(proposals[name])
    path='Assets/Assets/Scripts/Generated/'+name
    patch.extend(difflib.unified_diff(text(sources[name]).splitlines(True),text(proposals[name]).splitlines(True),fromfile='a/'+path,tofile='b/'+path))
    rows.append(dict(path=path,before=digest(sources[name]),after=digest(proposals[name])))
(out/'Proposed.patch').write_text(''.join(patch),encoding='utf-8')
(out/'Proposal.json').write_text(json.dumps(dict(status='OUTPUT_ONLY',changes=rows,evidence='No native speedup claim; exact immutable query input and original route extraction retained.'),indent=2))
(out/'LegacyMethod.cs.txt').write_text(old.replace('GetAcceptedRouteCenterline(', 'LegacyGetAcceptedRouteCenterline('))
(out/'QueryMethods.cs.txt').write_text(new)
print(json.dumps(rows))
