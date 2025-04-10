using UnityEngine;

public class Movement : MonoBehaviour
{
    public Transform joint1;
    public Transform joint2;
    public Transform target;

    public Transform target2;

    // Update is called once per frame
    void Update()
    {
        if (target != null)
        {
            // Make joint1 always face the target
            joint1.LookAt(target);
            joint1.eulerAngles = new Vector3(-90, joint1.eulerAngles.y - 180, 0);
        
            // Make joint2 always face the target
            joint2.LookAt(target);
            joint2.eulerAngles = new Vector3(joint2.eulerAngles.x + 270, joint1.eulerAngles.y - 180, joint1.eulerAngles.z-180);
        }
    }
}
