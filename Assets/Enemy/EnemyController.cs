using UnityEngine;

namespace Enemy
{
    public class EnemyController : MonoBehaviour
    {
        [Header("Movement")]
        [SerializeField] float moveSpeed = 5f;
        [SerializeField] float minStateTime = 0.5f;
        [SerializeField] float maxStateTime = 2f;

        private enum EnemyStateEnum
        {
            Idle,
            Walking,
            Running
        }
        
        private CharacterController controller;
    
        private Vector3 moveDirection;
        private EnemyStateEnum state;
        private float timeInState;
        private float stateTime;
    
        // Start is called once before the first execution of Update after the MonoBehaviour is created
        void Start()
        {
            controller = GetComponent<CharacterController>();
        }

        // Update is called once per frame
        void Update()
        {
            controller.Move(moveDirection.normalized * (moveSpeed * Time.deltaTime));
            transform.rotation = Quaternion.LookRotation(moveDirection);
            
            timeInState += Time.deltaTime;
            if (timeInState >= stateTime) ChangeState();
        }

        private void ChangeState()
        {
            
        }
    }
}
