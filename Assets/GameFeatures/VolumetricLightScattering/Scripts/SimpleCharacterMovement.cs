//Copyright(c) 2021 Razeware LLC

//Permission is hereby granted, free of charge, to any person
//obtaining a copy of this software and associated documentation
//files (the "Software"), to deal in the Software without
//restriction, including without limitation the rights to use,
//copy, modify, merge, publish, distribute, sublicense, and/or
//sell copies of the Software, and to permit persons to whom
//the Software is furnished to do so, subject to the following
//conditions:

//The above copyright notice and this permission notice shall be
//included in all copies or substantial portions of the Software.

//Notwithstanding the foregoing, you may not use, copy, modify,
//merge, publish, distribute, sublicense, create a derivative work,
//and/or sell copies of the Software in any work that is designed,
//intended, or marketed for pedagogical or instructional purposes
//related to programming, coding, application development, or
//information technology. Permission for such use, copying,
//modification, merger, publication, distribution, sublicensing,
//creation of derivative works, or sale is expressly withheld.

//This project and source code may use libraries or frameworks
//that are released under various Open-Source licenses. Use of
//those libraries and frameworks are governed by their own
//individual licenses.

//THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
//EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
//MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
//NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT
//HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY,
//WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
//OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER
//DEALINGS IN THE SOFTWARE.

using UnityEngine;

public class SimpleCharacterMovement : MonoBehaviour
{
    public float smooth = 1f;
    public float speed = 6.0f;
    public float jumpSpeed = 8.0f;
    public float gravity = 20.0f;

    private Animator anim;
    private CharacterController controller;

    private Quaternion lookLeft;
    private Quaternion lookRight;
    private Vector3 moveDirection = Vector3.zero;
    private Vector3 input = Vector3.zero;

    private void Start()
    {
        anim = GetComponentInChildren<Animator>();
        controller = GetComponent<CharacterController>();

        lookRight = transform.rotation;
        lookLeft = lookRight * Quaternion.Euler(0, 180, 0);
    }

    private void Update()
    {
        if (controller.isGrounded)
        {
            anim.SetBool("IsRunning", false);
            anim.SetBool("IsJumping", false);

            moveDirection = Vector3.zero;
            input = Input.GetAxis("Horizontal") * Vector3.forward;

            if (Input.GetKey(KeyCode.A))
            {
                transform.rotation = lookLeft;
                moveDirection = transform.TransformDirection(-input);

                anim.SetBool("IsRunning", true);
            }

            if (Input.GetKey(KeyCode.D))
            {
                transform.rotation = lookRight;
                moveDirection = transform.TransformDirection(input);

                anim.SetBool("IsRunning", true);
            }

            moveDirection *= speed;

            if (Input.GetButton("Jump"))
            {
                moveDirection.y = jumpSpeed;

                anim.SetBool("IsJumping", true);
            }
        }

        moveDirection.y -= gravity * Time.deltaTime;
        controller.Move(moveDirection * Time.deltaTime);
    }
}