using System;
using System.Collections;
using UnityEngine;
using TMPro;
using DG.Tweening;

public class MY_UIToastMassge_Script : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI _textMeshPro;
    [SerializeField] private Vector3 _movePos;
    [SerializeField] private float _duration = 1.0f;
    [SerializeField] private float _alphaDuration = 1.0f;

    private Vector3 _originPos;

    private bool _isActive; public bool is_Active => this._isActive;

    private void Start()
    {
        this._originPos = this.gameObject.GetComponent<RectTransform>().position;
    }

    //토스트 메시지 호출 시 애니메이션이 적용되어야 함.
    public void MassgeOpen_Func(string a_Massge)
    {
        StartCoroutine(this.ToastMassge_Update_Co(a_Massge));
    }

    private IEnumerator ToastMassge_Update_Co(string a_Massge)
    {
        this.Set_ToastMassgeStateUpdate_Func(true);
        this._textMeshPro.text = a_Massge;

        this.gameObject.GetComponent<RectTransform>().DOMove(this._originPos + this._movePos, this._duration);

        Color a_currentColor = new Color();

        while (true)
        {
            a_currentColor = this._textMeshPro.color;
            a_currentColor.a -= Time.deltaTime * this._alphaDuration;
            this._textMeshPro.color = a_currentColor;

            if (a_currentColor.a <= 0.0f)
                break;

            yield return null;
        }

        this.Set_ToastMassgeStateUpdate_Func(false);

        yield return null;
    }

    private void Set_ToastMassgeStateUpdate_Func(bool is_Open)
    {
        if(is_Open)
        {
            this._textMeshPro.color = Color.black;
            this._textMeshPro.text = "";
        }

        this._textMeshPro.gameObject.SetActive(is_Open);
        this._isActive = is_Open;
    }
}
