using UnityEngine;
using UnityEngine.UI;

// note: Exactly one native layout owner switches between carried-item tiles and variable-height generated records.
public sealed class YQBlueglassItemLayout : LayoutGroup
{
    private bool _grid;
    public bool Grid { get => _grid; set { if (_grid == value) return; _grid=value; SetDirty(); } }
    public override void CalculateLayoutInputHorizontal()
    {
        base.CalculateLayoutInputHorizontal();
        SetLayoutInputForAxis(_grid ? 400f : 0f,_grid ? 400f : 0f,1f,0);
    }
    public override void CalculateLayoutInputVertical()
    {
        float height=0f;
        if (_grid) height=Mathf.Max(0,Mathf.Ceil(rectChildren.Count/2f)*138f-12f);
        else foreach (RectTransform child in rectChildren) height+=Mathf.Max(28f,LayoutUtility.GetPreferredHeight(child))+8f;
        SetLayoutInputForAxis(height,height,0f,1);
    }
    public override void SetLayoutHorizontal()
    {
        // note: Two flexible columns leave room for actual item names at readable size and reserve the scroll gutter.
        float width=_grid ? Mathf.Max(0,(rectTransform.rect.width-12f)*.5f) : rectTransform.rect.width;
        for (int i=0;i<rectChildren.Count;i++)
            SetChildAlongAxis(rectChildren[i],0,_grid ? (i%2)*(width+12f) : 0f,width);
    }
    public override void SetLayoutVertical()
    {
        float y=0f;
        for (int i=0;i<rectChildren.Count;i++)
        {
            float height=_grid ? 126f : Mathf.Max(28f,LayoutUtility.GetPreferredHeight(rectChildren[i]));
            SetChildAlongAxis(rectChildren[i],1,_grid ? (i/2)*138f : y,height);
            y+=height+8f;
        }
    }
}
