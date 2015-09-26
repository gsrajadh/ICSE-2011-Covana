using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace TestApp
{    
    public class MySubClass
    {
        int mySubI;
        public int MySubI
        {
            get;
            set;
        }

        public void HandleAClass(AClass obj)
        {
        }
    }

    public class AClass
    {
    }

    public class MyClass
    {
        MySubClass sclass;
        AClass aclassobj;

        int i;
        
            
        int j
        {
            set; 
            get;
        }

        int k
        {
            set;
            get;
        }

        public MyClass()
        {
            sclass = new MySubClass();
            aclassobj = new AClass();
        }

        public void DefineIJK()
        {
            this.i = 10;
            this.j = 20;
            //this.k = 45;
            sclass.MySubI = 30;
        }

        public void AccessI()
        {
            int localVariable;
            Console.WriteLine("Testing " + this.i);
            localVariable = 20;
            Console.WriteLine(localVariable);
        }

        public void AccessJ()
        {
            Console.WriteLine("Testing " + this.j);
            Console.WriteLine("Testing " + this.k);
        }

        public void AccessIK()
        {
            //Console.WriteLine("Testing " + this.k);
            Console.WriteLine(this.i + this.j + sclass.MySubI);
        }

        public void NullCheckMethod()
        {
            if (sclass != null)
            {
            }
        }

        public void AccessOtherClass()
        {
            sclass.HandleAClass(aclassobj);
        }

        public MySubClass GetMySClass()
        {
            return this.sclass;
        }
    }
}
