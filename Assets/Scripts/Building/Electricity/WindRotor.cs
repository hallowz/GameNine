using UnityEngine;

namespace Voidborne.Building.Electricity
{
    /// <summary>
    /// Wind generator. Surface and sky islands only (Y > 0).
    /// Outputs 50–200W based on altitude. Higher = more output.
    /// </summary>
    public class WindRotor : PowerGenerator
    {
        [Header("Wind Rotor")]
        public float minOutput      = 50f;
        public float maxOutput      = 200f;
        public float altitudeForMax = 500f;  // Y at which full output is reached
        public float minimumY       = 0f;

        [Header("FX")]
        public Transform bladesTransform;
        public float     baseSpinSpeed = 90f; // degrees/s at min output

        protected override void Awake()
        {
            base.Awake();
        }

        protected override void Start()
        {
            if (transform.position.y < minimumY)
            {
                Debug.LogWarning("[WindRotor] Must be placed above Y = " + minimumY);
                _isGenerating = false;
                powerOutput = 0f;
            }
            else
            {
                _isGenerating = true;
                UpdateOutput();
            }
            base.Start();
        }

        private void Update()
        {
            if (_isGenerating)
            {
                UpdateOutput();
                SpinBlades();
            }
        }

        private void UpdateOutput()
        {
            float t = Mathf.Clamp01((transform.position.y - minimumY) / (altitudeForMax - minimumY));
            powerOutput = Mathf.Lerp(minOutput, maxOutput, t);
        }

        private void SpinBlades()
        {
            if (bladesTransform == null) return;
            float t = Mathf.Clamp01((powerOutput - minOutput) / (maxOutput - minOutput));
            float speed = Mathf.Lerp(baseSpinSpeed * 0.5f, baseSpinSpeed * 2f, t);
            bladesTransform.Rotate(Vector3.forward, speed * Time.deltaTime);
        }

        public float CurrentOutputWatts => _isGenerating ? powerOutput : 0f;

        public override float GetCurrentOutput() => _isGenerating ? powerOutput : 0f;
    }
}
