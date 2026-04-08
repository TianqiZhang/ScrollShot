using ScrollShot.Editor.Models;

namespace ScrollShot.Editor.Commands;

public sealed class CropCommand : IEditCommand
{
    private CropRect? _previousCrop;

    public CropCommand(CropRect? cropRect)
    {
        CropRect = cropRect;
    }

    public string Description => "Crop";

    public CropRect? CropRect { get; }

    public EditState Apply(EditState state)
    {
        _previousCrop = state.CropRect;
        return state with { CropRect = CropRect };
    }

    public EditState Undo(EditState state)
    {
        return state with { CropRect = _previousCrop };
    }
}
