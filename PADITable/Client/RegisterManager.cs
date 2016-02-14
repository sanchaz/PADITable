using System;
using IPADITable;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;

namespace Client
{
    class RegisterManager
    {
        //The list with the registers
        private Dictionary<Int32, string> mRegisters;
        //The max capacity of the dictionary (number of registers)
        private const int MAX_CAPACITY = 10;
        //The max register key value
        private const int MAX_REGISTER_KEY = 10;
        //The min register key value
        private const int MIN_REGISTER_KEY = 1;

        public RegisterManager()
        {
            mRegisters = new Dictionary<Int32, string>(MAX_CAPACITY);
           
            for (int i = 1; i <= MAX_CAPACITY; i++) { mRegisters.Add(i, null);  }
            IEnumerable<KeyValuePair<Int32,string>> ordered = mRegisters.OrderBy(element => element.Key);
            mRegisters = ordered.ToDictionary(x => x.Key, x => x.Value);
        }

        /// <summary>
        /// Gets a value for a register with register key registerKey
        /// </summary>
        /// <param name="registerKey"> The register key</param>
        /// <returns></returns>
        public string getRegisterValue(int registerKey)
        {
            if (registerKey >= MIN_REGISTER_KEY && registerKey <= MAX_REGISTER_KEY)
            {
                try
                {
                    string value;
                    mRegisters.TryGetValue(registerKey, out value);
                    if (value == null) { throw new ArgumentNullException(); }
                    return value;
                } catch (ArgumentNullException)
                {
                    System.Console.WriteLine("RegisterManager: getRegisterValue, the register " + registerKey + " has no value associated yet");
                    throw new RemoteException("The register " + registerKey + " has no value associated yet");
                }
            }
            else
            {
                System.Console.WriteLine("Register key must be in the range 1...10, gave " + registerKey);
                throw new RemoteException("Register key must be in the range 1...10, gave " + registerKey);
            }
        }

        /// <summary>
        /// Sets a value for a register with key registerKey
        /// </summary>
        /// <param name="registerKey">The register key</param>
        /// <param name="newValue">The new value</param>
        /// <returns></returns>
        public bool setRegisterValue(int registerKey, string newValue)
        {
            if (registerKey >= MIN_REGISTER_KEY && registerKey <= MAX_REGISTER_KEY)
            {
                mRegisters[registerKey] = newValue;
                return true;
            }
            else
            {
                System.Console.WriteLine("Could not set value for register, regNum: " + registerKey + " value: " + newValue);
                return false;
            }
        }

        /// <summary>
        /// Sets a value of a register to lower case
        /// </summary>
        /// <param name="registerKey">The register key</param>
        /// <returns></returns>
        public bool setRegisterToLower(int registerKey)
        {
            if (registerKey >= MIN_REGISTER_KEY && registerKey <= MAX_REGISTER_KEY && mRegisters.ContainsKey(registerKey))
            {
                mRegisters[registerKey] = mRegisters[registerKey].ToLower();
                
                //System.Console.WriteLine("Could not set value of register " + registerKey + " to lower");
                return true;
            }
            else
            {
                System.Console.WriteLine("Could not set value of register to lower, regNum: " + registerKey);
                return false;
            }
        }

        /// <summary>
        /// Sets a value of a register to upper case
        /// </summary>
        /// <param name="registerKey">The register key</param>
        /// <returns></returns>
        public bool setRegisterToUpper(int registerKey)
        {
            if (registerKey >= MIN_REGISTER_KEY && registerKey <= MAX_REGISTER_KEY && mRegisters.ContainsKey(registerKey))
            {
                mRegisters[registerKey] = mRegisters[registerKey].ToUpper();
                
                //System.Console.WriteLine("Could not set value of register " + registerKey + " to upper");
                return true;
            }
            else
            {
                System.Console.WriteLine("Could not set value of register to upper, regNum: " + registerKey);
                return false;
            }
        }

        /// <summary>
        /// Concats the values of two registers and stores in the first one
        /// </summary>
        /// <param name="registerKey1">The key of the first register</param>
        /// <param name="registerKey2">The key of the second register</param>
        /// <returns></returns>
        public bool concatRegisters(int registerKey1, int registerKey2)
        {
            if ((registerKey1 >= MIN_REGISTER_KEY && registerKey1 <= MAX_REGISTER_KEY) &&
                (registerKey2 >= MIN_REGISTER_KEY && registerKey2 <= MAX_REGISTER_KEY) && 
                mRegisters.ContainsKey(registerKey1) && mRegisters.ContainsKey(registerKey2))
            {
                mRegisters[registerKey1] = mRegisters[registerKey1] + mRegisters[registerKey2];
                

                //System.Console.WriteLine("Could not concat value of register " + registerKey1 + " with " + registerKey2);
                return true;
            }
            else
            {
                System.Console.WriteLine("Could not concatenate values of registers, reg1: " + registerKey1 + " reg2: " + registerKey2);
                return false;
            }
        }

        /// <summary>
        /// Prints the values of the registers
        /// </summary>
        public void print()
        {
            foreach(Int32 key in mRegisters.Keys)
            {
                if (mRegisters[key] == null) { System.Console.WriteLine(key + " N/D "); }
                else { System.Console.WriteLine(key + " " + mRegisters[key]); }
            }
        }

        /// <summary>
        /// Returns the values of all registers as a List
        /// </summary>
        public Dictionary<Int32, string> Registers { get { return mRegisters; } }
    }
}
