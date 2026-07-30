// The measurement period is a model-layer concept; it moved out of api/ so the
// model DTOs no longer import the api layer (2026-07-30 review, G4).

namespace MyAtm.Model;

public enum Period
{
    Minutes1, Minutes15, Hours1, Hours8, Hours24
}
