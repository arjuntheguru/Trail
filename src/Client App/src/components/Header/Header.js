import React, {useState} from 'react';
import { useSelector, useDispatch } from 'react-redux';
import { Link } from 'react-router-dom';
import { Navbar, Nav, Container, NavDropdown } from 'react-bootstrap';
import { logout } from './../../store/auth-actions';

import { FontAwesomeIcon } from '@fortawesome/react-fontawesome'
import { faBuilding, faStore, faTasks } from '@fortawesome/free-solid-svg-icons'

const Header = () => {

    const [show, setShow] = useState(false);

    const dispatch = useDispatch();

    const auth = useSelector(state => state.auth)

    const logoutHandler = () => {
        dispatch(logout());
    };

    return (
        <Navbar fixed='top' bg='dark' variant='dark'>
            <Container>
                <Link to="/" className='navbar-brand'>Food Safety Log</Link>
                <Nav className="mr-auto mt-1">
                    {
                        auth.isAuthenticated ?
                            auth.user.role === "SuperAdmin" ?
                                <>
                                    <Link to='/' className='nav-link text-light'>Home</Link>
                                    <NavDropdown title="Manage" className="text-info" show={show} onToggle={() => {setShow(!show)}}>
                                        <Link to='/company' className='dropdown-item'onClick={()=>{setShow(!show)}}>
                                            <FontAwesomeIcon icon={faBuilding} className="mr-2"/> Company
                                        </Link>   
                                        <Link to='/task' className='dropdown-item'onClick={()=>{setShow(!show)}}>
                                            <FontAwesomeIcon icon={faTasks} className="mr-2"/> Task
                                        </Link>                                       
                                    </NavDropdown>
                                </>
                                 : auth.user.role === 'Admin' ?
                                   <>
                                   <Link to='/' className='nav-link text-light'>Home</Link>
                                    <NavDropdown title="Manage" className="text-info" show={show} onToggle={() => {setShow(!show)}}>
                                        <Link to='/site' className='dropdown-item'onClick={()=>{setShow(!show)}}>
                                            <FontAwesomeIcon icon={faStore} className="mr-2"/> Site
                                        </Link>                                       
                                    </NavDropdown>
                                   </> : '' : ''
                    }
                </Nav>
                <Nav>
                    {!auth.isAuthenticated && <Link to="/" className='nav-link text-light'>Login</Link>}
                    {auth.isAuthenticated && <Link to="/" className='nav-link text-light' onClick={logoutHandler}>Log Out</Link>}
                </Nav>
            </Container>
        </Navbar>
    );
}

export default Header;