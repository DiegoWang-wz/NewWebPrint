namespace NewWebPrint.Utilities;

public class BLLBase
{
    public Storage cloudStorage = new Storage("server=192.168.1.248;user id=jcuser;password=Jiecang2019;database=AIS20170730;persistsecurityinfo=True");

    public Storage hskCodeStorage = new Storage("server=192.168.21.240;user id=sa;password=Jc..2015;database=HskCode;persistsecurityinfo=True");

    public Storage hskSqllite = new Storage("EntLibSQLite");

    public Storage controlCodeStorage = new Storage("server=192.168.3.248;user id=sa;password=JCDB@2025(..)sa;database=JCControlCode;persistsecurityinfo=True;Encrypt=True;TrustServerCertificate=True;");
}