using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

public class MY_UIPanelManager_Script : MonoBehaviour
{
    public static MY_UIPanelManager_Script Instance;

    public enum PanelType
    {
        None,
        Map_Panel,
        Diary_Panl,
        MAX
    }

    [SerializeField] private GameObject _mapPanel;
    [SerializeField] private GameObject _diaryPanel;
    [SerializeField] private GameObject _demmedPanel;

    private void Awake()
    {
        if(Instance == null)
            Instance = this;
    }

    public void Call_Dimmed_Func(bool a_IsDimmedOpen)
    {
        float a_fadeEndValue = a_IsDimmedOpen? 0.8f : 0.0f;

        if(a_IsDimmedOpen == true)
            this._demmedPanel.SetActive(a_IsDimmedOpen);

        this._demmedPanel.GetComponent<Image>().DOFade(a_fadeEndValue, 0.5f).OnComplete(() =>
        {
            if (a_IsDimmedOpen == false)
                this._demmedPanel.SetActive(a_IsDimmedOpen);
        });
    }

    public void PanelOpen_Func(PanelType a_PnelType, bool is_Open)
    {
        if(is_Open == true)
        {
            this._mapPanel.SetActive(false);
            this._diaryPanel.SetActive(false);
        }

        switch (a_PnelType)
        {
            case PanelType.None:
                break;
            case PanelType.Map_Panel:
                this._mapPanel.SetActive(is_Open);
                break;
            case PanelType.Diary_Panl:
                this._diaryPanel.SetActive(is_Open);
                break;
            case PanelType.MAX:
                break;
        }
    }
}
