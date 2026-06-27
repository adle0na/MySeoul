using NUnit.Framework;
using System.Collections.Generic;
using UnityEngine;

public class MY_UIToastMassgeManager_Script : MonoBehaviour
{
    public static MY_UIToastMassgeManager_Script Instance;

    [SerializeField] private GameObject _toastmassgePrefab;
    [SerializeField] private Transform _toastmassgeGroupTr;

    private List<MY_UIToastMassge_Script> _toastmassgeScriptList;

    private void Awake()
    {
        if(Instance == null)
            Instance = this;

        this._toastmassgeScriptList = new List<MY_UIToastMassge_Script>();
    }

    public void Call_ToastMassge_Func(string a_Massge)
    {
        if(0 < this._toastmassgeScriptList.Count)
        {
            for (int i = 0; i < this._toastmassgeScriptList.Count; i++)
            {
                if (this._toastmassgeScriptList[i].is_Active == false)
                {
                    this._toastmassgeScriptList[i].MassgeOpen_Func(a_Massge);
                    return;
                }
            }
        }
        else
        {
            GameObject go = GameObject.Instantiate(this._toastmassgePrefab);
            go.transform.SetParent(this._toastmassgeGroupTr);
            go.GetComponent<MY_UIToastMassge_Script>().MassgeOpen_Func(a_Massge);
            this._toastmassgeScriptList.Add(go.GetComponent<MY_UIToastMassge_Script>());
        }
    }

}
