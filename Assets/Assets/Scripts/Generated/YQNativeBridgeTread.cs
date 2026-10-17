using System;
using System.Collections.Generic;
using UnityEngine;

// note: This native-only geometry helper derives one visual/collision profile from the accepted route; it owns no plan, save, streamer or random state.
internal static class YQNativeBridgeTread
{
    private static List<int> Stations(IReadOnlyList<Vector3> road)
    {
        if(road==null || road.Count<2) throw new InvalidOperationException("Native bridge needs two bank stations.");
        var result=new List<int>();
        for(int index=0;index<road.Count;index++)
        {
            Vector3 point=road[index];
            if(float.IsNaN(point.x)||float.IsInfinity(point.x)||float.IsNaN(point.y)||float.IsInfinity(point.y)||float.IsNaN(point.z)||float.IsInfinity(point.z))
                throw new InvalidOperationException("Native bridge station is non-finite.");
            result.Add(index);
            // note: Coalesce only redundant three-dimensional samples, never a bend or a height breakpoint. Tiny collinear resampling intervals folded the weighted-tangent strip.
            while(result.Count>2)
            {
                int count=result.Count; Vector3 a=road[result[count-3]],b=road[result[count-2]],c=road[result[count-1]],delta=c-a;
                if(delta.sqrMagnitude<.0000000001f) break;
                float t=Vector3.Dot(b-a,delta)/delta.sqrMagnitude;
                if(t<=0f||t>=1f||(b-a-delta*t).sqrMagnitude>.0000000004f) break;
                result.RemoveAt(count-2);
            }
        }
        return result;
    }

    private static double HalfFloatUlp(float value)
    {
        int exponent = (BitConverter.SingleToInt32Bits(value) >> 23) & 255;
        return Math.Pow(2d, exponent == 0 ? -150 : exponent - 151);
    }

    internal static List<int> HorizontalStations(IReadOnlyList<Vector3> road, bool nativePrecision = true)
    {
        // note: Only direction descriptors absorb float representation uncertainty. Every original XYZ/height station remains in the profile and cross-section array.
        var indices = new List<int>();
        for (int index = 0; index < road.Count; index++)
        {
            indices.Add(index);
            while (indices.Count > 2)
            {
                int count = indices.Count;
                Vector3 pa = road[indices[count - 3]], pb = road[indices[count - 2]], pc = road[indices[count - 1]];
                if (!nativePrecision)
                {
                    // note: Archived geometry retains its original float predicate, including its existing tolerance.
                    Vector2 a = new Vector2(pa.x, pa.z), b = new Vector2(pb.x, pb.z), c = new Vector2(pc.x, pc.z), delta = c - a;
                    if (delta.sqrMagnitude < .0000000001f) break;
                    float t = Vector2.Dot(b - a, delta) / delta.sqrMagnitude;
                    if (t <= 0f || t >= 1f || (b - a - delta * t).sqrMagnitude > .0000000004f) break;
                }
                else
                {
                    double dx = (double)pc.x - pa.x, dz = (double)pc.z - pa.z, square = dx * dx + dz * dz;
                    if (square < .0000000001d) break;
                    double t = (((double)pb.x - pa.x) * dx + ((double)pb.z - pa.z) * dz) / square;
                    if (t <= 0d || t >= 1d) break;
                    double rx = (double)pb.x - pa.x - dx * t, rz = (double)pb.z - pa.z - dz * t;
                    double ex = HalfFloatUlp(pb.x) + (1d - t) * HalfFloatUlp(pa.x) + t * HalfFloatUlp(pc.x);
                    double ez = HalfFloatUlp(pb.z) + (1d - t) * HalfFloatUlp(pa.z) + t * HalfFloatUlp(pc.z);
                    if (rx * rx + rz * rz > Math.Max(.0000000004d, ex * ex + ez * ez)) break;
                }
                indices.RemoveAt(count - 2);
            }
        }
        return indices;
    }

    internal static bool HasEndpointMiterRunout(IReadOnlyList<Vector3> profile, float acceptedWidth, out bool extendStart, out bool extendEnd)
    {
        // note: Cropping inside a real corner miter cannot create a valid full-width end cap. Only the existing fitter may extend that endpoint onto further accepted dry ground.
        if (float.IsNaN(acceptedWidth) || float.IsInfinity(acceptedWidth) || acceptedWidth <= 0f)
            throw new InvalidOperationException("Native bridge width is invalid.");
        Stations(profile);
        var indices = HorizontalStations(profile);
        var knots = new List<Vector3>(indices.Count);
        foreach (int index in indices) knots.Add(profile[index]);
        extendStart = extendEnd = false;
        if (knots.Count < 3) return true;
        float half = Mathf.Max(1.2f, (acceptedWidth + 2f) * .5f);
        Vector2 firstLeg = new Vector2(knots[1].x - knots[0].x, knots[1].z - knots[0].z);
        int last = knots.Count - 1;
        Vector2 lastLeg = new Vector2(knots[last].x - knots[last - 1].x, knots[last].z - knots[last - 1].z);
        float firstLength = firstLeg.magnitude, lastLength = lastLeg.magnitude;
        if (firstLength <= 0f || lastLength <= 0f) throw new InvalidOperationException("Native bridge has a zero-length horizontal segment.");
        extendStart = firstLength <= half * Mathf.Abs(Vector2.Dot(MiterOffset(knots, 1), firstLeg / firstLength));
        extendEnd = lastLength <= half * Mathf.Abs(Vector2.Dot(MiterOffset(knots, last - 1), lastLeg / lastLength));
        return !extendStart && !extendEnd;
    }

    private static Vector2 MiterOffset(IReadOnlyList<Vector3> road,int index)
    {
        // note: A unit-direction bisector and its miter factor keep each adjoining segment's full accepted width; spacing cannot bias the section tangent.
        Vector2 At(int position)=>new Vector2(road[position].x,road[position].z);
        Vector2 incoming=index==0?At(1)-At(0):At(index)-At(index-1);
        Vector2 outgoing=index==road.Count-1?incoming:At(index+1)-At(index);
        if(incoming.sqrMagnitude<.00000001f||outgoing.sqrMagnitude<.00000001f)
            throw new InvalidOperationException("Native bridge has a zero-length horizontal segment.");
        incoming.Normalize(); outgoing.Normalize(); Vector2 tangent=incoming+outgoing;
        if(tangent.sqrMagnitude<.0001f) throw new InvalidOperationException("Native bridge reverses inside its tread width.");
        tangent.Normalize(); Vector2 side=new Vector2(-tangent.y,tangent.x);
        float projection=Vector2.Dot(side,new Vector2(-outgoing.y,outgoing.x));
        if(projection<.25f) throw new InvalidOperationException("Native bridge miter exceeds its bounded footprint.");
        return side/projection;
    }

    private static Vector2[] SectionOffsets(IReadOnlyList<Vector3> road,Vector2? first,Vector2? last,bool nativePrecision=true)
    {
        // note: Horizontal knots describe the corridor only. Keep every vertical profile station, including a height break inside a corner's miter, in the fitted and published surface.
        if(road==null||road.Count<2) throw new InvalidOperationException("Native bridge needs two bank stations.");
        var indices=HorizontalStations(road,nativePrecision);
        var knots=new List<Vector3>(indices.Count); foreach(int index in indices) knots.Add(road[index]);
        var offsets=new Vector2[knots.Count];
        for(int index=0;index<offsets.Length;index++)
            offsets[index]=index==0&&first.HasValue?first.Value:index==offsets.Length-1&&last.HasValue?last.Value:MiterOffset(knots,index);
        var result=new Vector2[road.Count]; int segment=0;
        for(int index=0;index<road.Count;index++)
        {
            while(segment<indices.Count-2&&index>indices[segment+1]) segment++;
            Vector2 a=new Vector2(knots[segment].x,knots[segment].z),b=new Vector2(knots[segment+1].x,knots[segment+1].z);
            Vector2 point=new Vector2(road[index].x,road[index].z),delta=b-a;
            if(delta.sqrMagnitude<.00000001f) throw new InvalidOperationException("Native bridge has a zero-length horizontal segment.");
            float t=Mathf.Clamp01(Vector2.Dot(point-a,delta)/delta.sqrMagnitude);
            // note: Both knot miters project to unit width on this straight leg. Interpolation subdivides its valid trapezoid instead of folding a short independent perpendicular section into the corner.
            result[index]=Vector2.Lerp(offsets[segment],offsets[segment+1],t);
        }
        return result;
    }

    internal static Vector2 SectionOffset(IReadOnlyList<Vector3> road,int index)
    {
        // note: Recursive arch sections must inherit the same whole-road offset field as fitting and mesh construction, including stations inside a straight leg's miter transition.
        return SectionOffsets(road,null,null)[index];
    }

    private static Vector2[,] CrossSections(IReadOnlyList<Vector3> road,float width,Vector2? first,Vector2? last,bool nativePrecision=true)
    {
        if(float.IsNaN(width)||float.IsInfinity(width)||width<=0f) throw new InvalidOperationException("Native bridge width is invalid.");
        float half=Mathf.Max(1.2f,width*.5f); var sections=new Vector2[road.Count,2];
        Vector2[] offsets=SectionOffsets(road,first,last,nativePrecision);
        Vector2 origin=new Vector2(road[0].x,road[0].z);
        for(int index=0;index<road.Count;index++)
        {
            Vector2 side=offsets[index]*half;
            Vector2 center=new Vector2(road[index].x,road[index].z)-origin;
            sections[index,0]=center-side; sections[index,1]=center+side;
        }
        return sections;
    }

    private static float TriangleAltitude(Vector2 a,Vector2 b,Vector2 c,float baseLength)
    {
        Vector2 u=b-a,v=c-a;
        double area=(double)u.y*v.x-(double)u.x*v.y;
        if(area<=.00000001d) throw new InvalidOperationException("Native bridge full-width strip folds or degenerates.");
        return (float)(area/baseLength);
    }

    internal static List<Vector3> FitProfile(IReadOnlyList<Vector3> source,float width,float maximumGradeDegrees)
    {
        if(float.IsNaN(width)||float.IsInfinity(width)||width<=0f) throw new InvalidOperationException("Native bridge width is invalid.");
        if(float.IsNaN(maximumGradeDegrees)||float.IsInfinity(maximumGradeDegrees)||maximumGradeDegrees<=0f||maximumGradeDegrees>=90f)
            throw new InvalidOperationException("Native bridge accepted grade is invalid.");
        List<int> indices=Stations(source); var road=new List<Vector3>(indices.Count);
        foreach(int index in indices) road.Add(source[index]);
        // note: The wider shoulder determines the binding grade. Each top triangle has exactly two station heights, so its perpendicular altitude gives the exact full-width slope constraint.
        Vector2[,] sections=CrossSections(road,width+2f,null,null); var allowed=new float[road.Count-1];
        float grade=Mathf.Tan(maximumGradeDegrees*Mathf.Deg2Rad);
        for(int index=0;index<allowed.Length;index++)
        {
            Vector2 a=sections[index,0],b=sections[index,1],c=sections[index+1,0],d=sections[index+1,1];
            allowed[index]=grade*Mathf.Min(TriangleAltitude(a,b,c,(b-a).magnitude),TriangleAltitude(b,d,c,(d-c).magnitude));
        }
        float[] heights=new float[road.Count]; for(int index=0;index<road.Count;index++) heights[index]=road[index].y;
        for(int index=1;index<road.Count;index++) heights[index]=Mathf.Max(heights[index],heights[index-1]-allowed[index-1]);
        for(int index=road.Count-2;index>=0;index--) heights[index]=Mathf.Max(heights[index],heights[index+1]-allowed[index]);
        // note: Retain every original sample and section count for supported visual arches. Only its derived height rises; coalesced collision stations interpolate the same resolved profile.
        var result=new List<Vector3>(source.Count); int segment=0;
        for(int index=0;index<source.Count;index++)
        {
            while(segment<indices.Count-2&&index>indices[segment+1]) segment++;
            Vector2 a=new Vector2(road[segment].x,road[segment].z),b=new Vector2(road[segment+1].x,road[segment+1].z),p=new Vector2(source[index].x,source[index].z);
            Vector2 delta=b-a; float t=Mathf.Clamp01(Vector2.Dot(p-a,delta)/delta.sqrMagnitude);
            Vector3 point=source[index]; point.y=Mathf.Max(point.y,Mathf.Lerp(heights[segment],heights[segment+1],t)); result.Add(point);
        }
        return result;
    }

    internal static Mesh Build(IReadOnlyList<Vector3> source,float width,float topOffset,float bottomOffset,bool capEnds,string name,
        Vector2? first=null,Vector2? last=null,bool nativePrecision=false)
    {
        List<int> indices=Stations(source); var road=new List<Vector3>(indices.Count); foreach(int index in indices) road.Add(source[index]);
        Vector2[,] sections=CrossSections(road,width,first,last,nativePrecision); Vector2 origin=new Vector2(road[0].x,road[0].z);
        int count=road.Count; var vertices=new Vector3[count*4]; var triangles=new int[(count-1)*24+(capEnds?12:0)];
        for(int index=0;index<count;index++)
        {
            int at=index*4;
            vertices[at]=new Vector3(origin.x+sections[index,0].x,road[index].y+topOffset,origin.y+sections[index,0].y);
            vertices[at+1]=new Vector3(origin.x+sections[index,1].x,road[index].y+topOffset,origin.y+sections[index,1].y);
            vertices[at+2]=vertices[at]+Vector3.down*(topOffset-bottomOffset); vertices[at+3]=vertices[at+1]+Vector3.down*(topOffset-bottomOffset);
        }
        for(int index=0;index<count-1;index++)
        {
            int a=index*4,b=(index+1)*4,t=index*24;
            TriangleAltitude(sections[index,0],sections[index,1],sections[index+1,0],(sections[index,1]-sections[index,0]).magnitude);
            TriangleAltitude(sections[index,1],sections[index+1,1],sections[index+1,0],(sections[index+1,1]-sections[index+1,0]).magnitude);
            // note: Wind the tread upward, underside downward and outer walls outward, with no internal bank caps.
            int[] faces={a,a+1,b,a+1,b+1,b,a+2,b+2,a+3,a+3,b+2,b+3,a,b,a+2,b,b+2,a+2,a+1,a+3,b+1,a+3,b+3,b+1};
            Array.Copy(faces,0,triangles,t,faces.Length);
        }
        if(capEnds)
        {
            int end=(count-1)*4,t=(count-1)*24; int[] faces={0,2,1,1,2,3,end,end+1,end+2,end+1,end+3,end+2}; Array.Copy(faces,0,triangles,t,faces.Length);
        }
        var mesh=new Mesh { name=name,vertices=vertices,triangles=triangles }; mesh.RecalculateNormals(); mesh.RecalculateBounds(); return mesh;
    }
}
