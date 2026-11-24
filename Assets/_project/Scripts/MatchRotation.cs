using UnityEngine;

public class MatchRotation : MonoBehaviour
{

    [SerializeField] Transform _target; // Start is called once before the first execution of Update after the MonoBehaviour is created
    void LateUpdate()
    {
        if (_target != null)
        {
            transform.rotation = _target.rotation;
        }
        
    }
}
