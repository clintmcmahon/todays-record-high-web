import Row from "react-bootstrap/Row";
import Col from "react-bootstrap/Col";
import TopNav from "../components/navigation/TopNav";
function Privacy() {
  return (
    <>
      <TopNav hideLocation={true} />
      <div className="container-fluid">
        <Row className="mb-4 mt-4">
          <Col xs={12} className="mt-2">
            <article>
              <h1 dir="auto">Privacy Policy</h1>
              <p dir="auto">
                We take your privacy seriously. To better protect your privacy
                we provide this privacy policy notice explaining the way your
                personal information is collected and used.
              </p>
              <h2 dir="auto">Collection of Routine Information</h2>
              <p dir="auto">
                This website and app track basic information about their users.
                This information includes, but is not limited to, IP addresses,
                browser details, timestamps and referring pages. None of this
                information can personally identify specific user to this
                website or app. The information is tracked for routine
                administration and maintenance purposes. This information is not
                for sale and will never be sold.
              </p>
              <h2 dir="auto">Cookies</h2>
              <p dir="auto">
                Where necessary, this website uses cookies to store information
                about a visitor’s preferences and history in order to better
                serve the user and/or present the user with customized content.
              </p>
              <h2 dir="auto">Links to Third Party Websites</h2>
              <p dir="auto">
                We have included links on this app for your use and reference.
                We are not responsible for the privacy policies on these
                websites. You should be aware that the privacy policies of these
                websites may differ from our own.
              </p>
              <h2 dir="auto">Security</h2>
              <p dir="auto">
                The security of your personal information is important to us,
                but remember that no method of transmission over the Internet,
                or method of electronic storage, is 100% secure. While we strive
                to use commercially acceptable means to protect your personal
                information, we cannot guarantee its absolute security.
              </p>
              <h2 dir="auto">Changes To This Privacy Policy</h2>
              <p dir="auto">
                This Privacy Policy is effective as of November 5th 2022 and
                will remain in effect except with respect to any changes in its
                provisions in the future, which will be in effect immediately
                after being posted on this page.
              </p>
              <p dir="auto">
                We reserve the right to update or change our Privacy Policy at
                any time and you should check this Privacy Policy periodically.
                If we make any material changes to this Privacy Policy, we will
                notify you either through the email address you have provided
                us, or by placing a prominent notice on our app.
              </p>
              <h2 dir="auto">Contact Information</h2>
              <p dir="auto">
                For any questions or concerns regarding the privacy policy,
                please send us an email to clint@msoftwaredev.com.
              </p>
            </article>
          </Col>
        </Row>
      </div>
    </>
  );
}

export default Privacy;
