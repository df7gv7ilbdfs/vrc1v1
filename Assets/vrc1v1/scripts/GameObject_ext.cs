using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

public static class GameObject_ext
{
    public static GameObject GetComponentInChildrenByName(this GameObject gameObject, string name)
    {
        var t = gameObject.transform.Find(name);

        if(t == null)
        {
            return null;
        }

        return t.gameObject;
    }
}
