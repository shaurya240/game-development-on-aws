using UnityEngine;
public class AIController : MonoBehaviour {
    Renderer rend;
    public Circuit circuit;
    public float brakingSensitivity = 1.1f;
    public float steeringSensitivity = 0.01f;
    public float accelSensitivity = 0.3f;
    Drive ds;
    Vector3 target;
    Vector3 nextTarget;
    int currentWP = 0;
    float totalDistanceToTarget;

    GameObject tracker;
    int currentTrackerWP = 0;
    public float lookAhead = 10;

    float lastTimeMoving = 0.0f;

    CheckpointManager cpm;
    float finishSteer;

    void Start() {

        if (circuit == null) {

            circuit = GameObject.FindGameObjectWithTag("circuit").GetComponent<Circuit>();
        }

        for (int i = 0; i < circuit.waypoints.Length; ++i) {

            circuit.waypoints[i].SetActive(false);
        }
        ds = this.GetComponent<Drive>();
        target = circuit.waypoints[currentWP].transform.position;
        nextTarget = circuit.waypoints[currentWP + 1].transform.position;
        totalDistanceToTarget = Vector3.Distance(target, ds.rb.gameObject.transform.position);

        tracker = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        DestroyImmediate(tracker.GetComponent<Collider>());
        tracker.GetComponent<MeshRenderer>().enabled = false;
        tracker.transform.position = ds.rb.gameObject.transform.position;
        tracker.transform.rotation = ds.rb.gameObject.transform.rotation;
        this.GetComponent<Ghost>().enabled = false;
        finishSteer = Random.Range(-1.0f, 1.0f);
    }

    void ProgressTracker() {

        Debug.DrawLine(ds.rb.gameObject.transform.position, tracker.transform.position);

        if (Vector3.Distance(ds.rb.gameObject.transform.position, tracker.transform.position) > lookAhead) {

            return;
        }

        tracker.transform.LookAt(circuit.waypoints[currentTrackerWP].transform.position);
        tracker.transform.Translate(0.0f, 0.0f, 1.0f);  // Speed of the tracker;

        if (Vector3.Distance(tracker.transform.position, circuit.waypoints[currentTrackerWP].transform.position) < 1.0f) {

            currentTrackerWP++;
            if (currentTrackerWP >= circuit.waypoints.Length) {

                currentTrackerWP = 0;
            }
        }
    }

    void ResetLayer() {

        ds.rb.gameObject.layer = 0;
        this.GetComponent<Ghost>().enabled = false;
    }

    void Update() {
        if (!RaceMonitor.racing) {

            lastTimeMoving = Time.time;
            return;
        }


        if (cpm == null) {

            cpm = ds.rb.GetComponent<CheckpointManager>();
        }

        if (cpm.lap == RaceMonitor.totalLaps + 1) {

            ds.highAccel.Stop();
            ds.Go(0, finishSteer, 0);
            return;
        }

        ProgressTracker();

        Vector3 localTarget;
        float targetAngle;

        if (ds.rb.velocity.magnitude > 1.0f) {

            lastTimeMoving = Time.time;
        }

        if (this.transform.position.y < -2.0f) {

            Debug.Log("Off the track");
        }

        if (Time.time > lastTimeMoving + 4 || ds.rb.gameObject.transform.position.y < -5.0f) {


            ds.rb.gameObject.transform.position = cpm.lastCP.transform.position + Vector3.up * 2.0f;
            ds.rb.gameObject.transform.rotation = cpm.lastCP.transform.rotation;
            //circuit.waypoints[currentTrackerWP].transform.position + Vector3.up * 2 +
            //new Vector3(Random.Range(-1.0f, 1.0f), 0.0f, Random.Range(-1.0f, 1.0f));
            tracker.transform.position = cpm.lastCP.transform.position;
            ds.rb.gameObject.layer = 8;
            this.GetComponent<Ghost>().enabled = true;
            Invoke("ResetLayer", 3);
        }

        if (Time.time < ds.rb.GetComponent<AvoidDetector>().avoidTime) {

            localTarget = tracker.transform.right * ds.rb.GetComponent<AvoidDetector>().avoidPath;
        } else {

            localTarget = ds.rb.gameObject.transform.InverseTransformPoint(tracker.transform.position);
        }
        targetAngle = Mathf.Atan2(localTarget.x, localTarget.z) * Mathf.Rad2Deg;

        float steer = Mathf.Clamp(targetAngle * steeringSensitivity, -1.0f, 1.0f) * Mathf.Sign(ds.currentSpeed);

        float speedFactor = ds.currentSpeed / ds.maxSpeed;

        float corner = Mathf.Clamp(Mathf.Abs(targetAngle), 0.0f, 90.0f);
        float cornerFactor = corner / 90.0f;

        float brake = 0.0f;
        if (corner > 10 && speedFactor > 0.1f) {

            brake = Mathf.Lerp(0.0f, 1.0f + speedFactor * brakingSensitivity, cornerFactor);
        }

        float accel = 1.0f;
        if (corner > 20.0f && speedFactor > 0.4f) {

            accel = Mathf.Lerp(0.0f, 1.0f * accelSensitivity, 1 - cornerFactor);
        }

        float prevTorque = ds.torque;
        if (speedFactor < 0.3f && ds.rb.gameObject.transform.forward.y > 0.2f) {

            ds.torque *= 3.0f;
            accel = 1.0f;
            brake = 0.0f;
        }

        if (!RaceMonitor.racing) accel = 0.0f;
        ds.Go(accel, steer, brake);

        ds.CheckForSkid();
        ds.CalculateEngineSound();

        ds.torque = prevTorque;
    }
}
