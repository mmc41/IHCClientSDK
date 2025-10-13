using System;
using Avalonia.Controls.Primitives;

namespace ihc_lab;

public class DynCtrl : TemplatedControl
{
    private Type typeForControl = typeof(string);
     public Type TypeForControl
    {
        get { return typeForControl; }
        set { typeForControl=value; }
    }
}