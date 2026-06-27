using NUnit.Framework;
using System;
using System.Collections.Generic;
using UnityEditor.Purchasing;
using UnityEngine;

public class MY_UIButtonManager_Script : MonoBehaviour
{
    public static MY_UIButtonManager_Script Instance;

    [SerializeField] private Sprite null_Img;
    [SerializeField] private List<ButtonInfo_Class> _buttoninfoList;
    private Dictionary<UIButtonType, ButtonInfo_Class> _buttonListDictionary;

    public enum UIButtonType
    {
        None,
        BagOpen,
        Setting,
        MAX
    }

    private void Awake()
    {
        if (Instance == null)
            Instance = this;

        this._buttonListDictionary = new Dictionary<UIButtonType, ButtonInfo_Class>();

        for (int i = 0; i < this._buttoninfoList.Count; i++)
        {
            if (this._buttoninfoList[i]._buttonSprite == null)
                this._buttoninfoList[i]._buttonSprite = null_Img;

            if (this._buttoninfoList[i]._buttonBackSprite == null)
                this._buttoninfoList[i]._buttonBackSprite = null_Img;

            this._buttonListDictionary.Add(this._buttoninfoList[i]._buttonType, this._buttoninfoList[i]);
        }
    }

    public ButtonInfo_Class Get_UIButtonSpriteTypeToSprite_Func(UIButtonType a_SpriteType)
    {
        this._buttonListDictionary.TryGetValue(a_SpriteType, out ButtonInfo_Class a_Value);

        if(a_Value != null)
        {
            return a_Value;
        }
        else
        {
            return null;
        }
    }
}

[Serializable]
public class ButtonInfo_Class
{
    public Sprite _buttonSprite;
    public Sprite _buttonBackSprite;
    public string _buttonName;
    public MY_UIButtonManager_Script.UIButtonType _buttonType;
}
