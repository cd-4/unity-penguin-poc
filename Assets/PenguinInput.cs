using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// There is currently no reason for this to exist
public class PenguinInput
{

    static PenguinControls inputGlobal = null;

    public static PenguinControls Get()
    {
        if (inputGlobal == null)
        {
            inputGlobal = new PenguinControls();
        }
        return inputGlobal;
    }

    // Start is called before the first frame update
    void Start()
    {

    }

    // Update is called once per frame
    void Update()
    {

    }
}
