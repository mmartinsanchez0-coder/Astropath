using UnityEngine;

public class PlanetSpawner : MonoBehaviour
{
    [Header("Planet")]
    [SerializeField] GameObject _planetPrefab;

    [Header("Ship")]
    [SerializeField] Transform _ship;

    [Header("Visual distance")]
    [SerializeField] float _spawnDistance = 5000f;

    GameObject _currentPlanet;

    void Start()
    {
        if (_planetPrefab == null || _ship == null)
        {
            Debug.LogError(
                "PlanetSpawner: asigna el Prefab y la nave en el Inspector."
            );
            return;
        }

        Vector3 spawnPosition =
            _ship.position + _ship.forward * _spawnDistance;

        _currentPlanet = Instantiate(
            _planetPrefab,
            spawnPosition,
            Quaternion.identity
        );

        _currentPlanet.name = "Mercury_Runtime";
    }
}
