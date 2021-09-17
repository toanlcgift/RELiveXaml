﻿using System;
using System.Security.Cryptography;
using System.Text;

namespace CrackLiveXAML
{
  public class XamlHash
  {
    public static string Get(string xaml)
    {
      return BitConverter.ToString(MD5.Create().ComputeHash(Encoding.Unicode.GetBytes(xaml)));
    }
  }
}
